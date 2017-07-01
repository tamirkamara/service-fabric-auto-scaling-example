using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonitorService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class MonitorService : StatelessService
    {
        public MonitorService(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ServiceScalingPlan> serviceScalingPlan = Utils.GetServiceScalingPlan(); //normally we'd want to refresh this from time to time

                try
                {
                    var fc = new FabricClient();

                    var applicationList = fc.QueryManager.GetApplicationListAsync().Result;

                    //iterate over applications that are in Ready state
                    foreach (var application in applicationList.Where(x => x.ApplicationStatus == System.Fabric.Query.ApplicationStatus.Ready))
                    {
                        // get all services
                        var serviceList = fc.QueryManager.GetServiceListAsync(application.ApplicationName).Result;

                        // group services by their type
                        var serviceTypeGroups = serviceList.GroupBy(x => x.ServiceTypeName);

                        foreach (var serviceTypeGroup in serviceTypeGroups)
                        {
                            // filter the rules according to the service type name
                            var serviceScalingScheme = serviceScalingPlan.Where(x => x.ServiceTypeName == serviceTypeGroup.Key).SingleOrDefault();

                            // no rules for this service type
                            if (serviceScalingScheme == null)
                            {
                                continue;
                            }

                            // get service description for all services of the current service type
                            List<StatelessServiceDescription> serviceDescriptionList = new List<StatelessServiceDescription>();

                            foreach (var service in serviceTypeGroup)
                            {
                                var serviceDescription = fc.ServiceManager.GetServiceDescriptionAsync(service.ServiceName).Result;

                                // we should scale only stateless services
                                if (serviceDescription.Kind == ServiceDescriptionKind.Stateless)
                                {
                                    serviceDescriptionList.Add((StatelessServiceDescription)serviceDescription);
                                }
                            }

                            // sum the total number of instances for the current service type
                            int currentInstanceCount = serviceDescriptionList.Sum(x => x.InstanceCount);

                            // check which of the rules applies
                            foreach (var rule in serviceScalingScheme.ScalingRules)
                            {
                                int newInstanceCount = currentInstanceCount + rule.InstaceCountChange;

                                // verify the new count is not out of bounds
                                if (newInstanceCount < serviceScalingScheme.MinInstanceCount)
                                {
                                    newInstanceCount = serviceScalingScheme.MinInstanceCount;
                                }

                                if (newInstanceCount < 1)
                                {
                                    newInstanceCount = 1;
                                }

                                if (newInstanceCount > serviceScalingScheme.MaxInstanceCount)
                                {
                                    newInstanceCount = serviceScalingScheme.MaxInstanceCount;

                                    Utils.isGoingUp = false;
                                }


                                int instanceDelta = newInstanceCount - currentInstanceCount;
                                if (instanceDelta == 0) // no need to do anything...
                                {
                                    continue;
                                }

                                // get the metric value according to which we want to scale. probably this would need to change and be more dynamic
                                long metricValue = Utils.GetMetricValue();

                                switch (rule.MetricOperand)
                                {
                                    case ScalingRule.MetricOperandType.GreaterThan:
                                        if (metricValue > rule.MetricThroshold)
                                        {
                                            ScaleServiceOut(serviceDescriptionList, instanceDelta, fc);
                                        }
                                        break;
                                    case ScalingRule.MetricOperandType.LessThan:
                                        if (metricValue < rule.MetricThroshold)
                                        {
                                            ScaleServiceIn(serviceDescriptionList, instanceDelta, fc);
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("EXCEPTION scaling operation failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        private static void ScaleServiceOut(List<StatelessServiceDescription> serviceDesciptionList, int instanceDelta, FabricClient fc)
        {
            ServiceEventSource.Current.Message(
                    String.Format("Starting to scale out service type {0} with total instance count delta {1}", serviceDesciptionList.First().ServiceTypeName, instanceDelta));

            int clusterNodeCount = fc.QueryManager.GetNodeListAsync().Result.Count();

            int remainingDelta = instanceDelta;

            foreach (var serviceDescription in serviceDesciptionList.OrderBy(x => x.ServiceName.ToString()))
            {
                if (serviceDescription.InstanceCount >= clusterNodeCount)
                {
                    // we can't add more to this service
                    continue;
                }

                int newInstanceCount = Math.Min(serviceDescription.InstanceCount + remainingDelta, clusterNodeCount);

                UpdateServiceInstanceCount(serviceDescription, newInstanceCount, fc);

                remainingDelta = remainingDelta - (newInstanceCount - serviceDescription.InstanceCount);

                ServiceEventSource.Current.Message(
                    String.Format("Remaining delta for service type {0} is {1}", serviceDescription.ServiceTypeName, remainingDelta));
            }

            // if we still have outstanding delta, it means we need to create more services of the same type
            while (remainingDelta > 0)
            {
                var newServiceDesc = serviceDesciptionList.Last();

                newServiceDesc.ServiceName = new Uri(Utils.GetNextServiceName(newServiceDesc.ServiceName));
                int newInstanceCount = Math.Min(remainingDelta, clusterNodeCount);

                newServiceDesc.InstanceCount = newInstanceCount;

                fc.ServiceManager.CreateServiceAsync(newServiceDesc).Wait();
                ServiceEventSource.Current.Message(string.Format("New service {0} has been created successfully", newServiceDesc.ServiceName));

                remainingDelta = remainingDelta - newInstanceCount;

                ServiceEventSource.Current.Message(
                    String.Format("Remaining delta for service type {0} is {1}", newServiceDesc.ServiceTypeName, remainingDelta));
            }

            ServiceEventSource.Current.Message(
                    String.Format("Scaling for service type {0} is done", serviceDesciptionList.First().ServiceTypeName));
        }


        private static void ScaleServiceIn(List<StatelessServiceDescription> serviceDesciptionList, int instanceDelta, FabricClient fc)
        {
            ServiceEventSource.Current.Message(
                    String.Format("Starting to scale in service type {0} with total instance count delta {1}", serviceDesciptionList.First().ServiceTypeName, instanceDelta));

            int remainingDelta = instanceDelta;

            foreach (var serviceDescription in serviceDesciptionList.OrderByDescending(x => x.ServiceName.ToString()))
            {
                int newInstanceCount = serviceDescription.InstanceCount + remainingDelta;

                if (newInstanceCount >= 1)
                {
                    UpdateServiceInstanceCount(serviceDescription, newInstanceCount, fc);

                    remainingDelta = remainingDelta - (newInstanceCount - serviceDescription.InstanceCount);
                }
                else
                {// this means we need to delete the current service
                    DeleteServiceDescription dsd = new DeleteServiceDescription(serviceDescription.ServiceName);

                    remainingDelta = remainingDelta - (0 - serviceDescription.InstanceCount);

                    fc.ServiceManager.DeleteServiceAsync(dsd).Wait();

                    ServiceEventSource.Current.Message(
                        String.Format("Deleted service {0}", serviceDescription.ServiceTypeName));
                }

                ServiceEventSource.Current.Message(
                    String.Format("Remaining delta for service type {0} is {1}", serviceDescription.ServiceTypeName, remainingDelta));
            }

            ServiceEventSource.Current.Message(
                    String.Format("Scaling for service type {0} is done", serviceDesciptionList.First().ServiceTypeName));
        }

        private static void UpdateServiceInstanceCount(StatelessServiceDescription serviceDescription, int newInstanceCount, FabricClient fc)
        {
            StatelessServiceUpdateDescription sud = new StatelessServiceUpdateDescription();
            sud.InstanceCount = newInstanceCount;

            fc.ServiceManager.UpdateServiceAsync(serviceDescription.ServiceName, sud).Wait();

            ServiceEventSource.Current.Message(
                String.Format("Service {0} instance count changed from {1} to {2}", serviceDescription.ServiceName, serviceDescription.InstanceCount, sud.InstanceCount));

        }
    }
}
