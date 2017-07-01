using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorService
{
    class Utils
    {
        public static string GetNextServiceName(Uri serviceUri)
        {
            string name = serviceUri.ToString();

            string newName;

            if (serviceUri.Segments.Last().Contains("_"))
            {
                int lastIndex = name.LastIndexOf('_');
                string baseName = name.Substring(0, lastIndex);
                int lastServiceNumber = int.Parse(name.Substring(lastIndex + 1, 3));
                newName = string.Format("{0}_{1}", baseName, lastServiceNumber + 1);
            }
            else
            {
                newName = string.Format("{0}_{1}", name, 1);
            }

            return newName;
        }

        public static List<ServiceScalingPlan> GetServiceScalingPlan()
        {
            //this is an example
            List<ScalingRule> scalingRules = new List<ScalingRule>();

            ScalingRule sr = new ScalingRule
            {
                MetricThroshold = 10,
                MetricOperand = ScalingRule.MetricOperandType.GreaterThan,
                InstaceCountChange = 1
            };
            scalingRules.Add(sr);

            sr = new ScalingRule
            {
                MetricThroshold = 5,
                MetricOperand = ScalingRule.MetricOperandType.LessThan,
                InstaceCountChange = -1
            };
            scalingRules.Add(sr);


            List<ServiceScalingPlan> serviceScalingList = new List<ServiceScalingPlan>();

            ServiceScalingPlan serviceScaling = new ServiceScalingPlan
            {
                ServiceTypeName = "Stateless1Type",
                ScalingRules = scalingRules,
                MinInstanceCount = 1,
                MaxInstanceCount = 7
            };

            serviceScalingList.Add(serviceScaling);

            return serviceScalingList;
        }


        // change this to your actual metric
        public static bool isGoingUp = true;

        public static long GetMetricValue()
        {
            if (isGoingUp)
                return 11;
            else
                return 4;
        }

        public static long GetQueueMsgCount()
        {
            try
            {
                string connectionString = @"Endpoint=sb://[x].servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[y]";
                var namespaceManager = Microsoft.ServiceBus.NamespaceManager.CreateFromConnectionString(connectionString);
                long count = namespaceManager.GetQueue("myQueue").MessageCount;

                return count;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        //public static string GetConfigParamValue()
        //{
        //    var configurationPackage = Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");

        //    var connectionStringParameter = configurationPackage.Settings.Sections["UserDatabase"].Parameters["UserDatabaseConnectionString"];
        //}
    }
}
