# Service Fabric Auto Scaling Service #

This is an example of how to implement an auto scaling service within Service Fabric. 

The solution is comprised of a few services:
* `MonitorService` - this is the actual auto scaling service
* `Stateless1` - a getting started service that would be scaled

When running this example on your local 5 node cluster, the Stateless service would start with 4 instances and be scaled to 7 (1 at a time), and then be scaled back to only 1 instance. 
The `MonitorService` is using a dummy metric when deciding wheter to scalue in/out, that value is coming from the `Utils.GetMetricValue` method.
Scaling rules are defined in the `Utils.GetServiceScalingPlan` method where as the metric being examined is fetch in the Utils.GetMetricValue. Both these methods are pretty simple and were written just to test the auto scaling functionality.

## HyperScaling ##
The number of instances allowed for each service (aka InstanceCount) is limited to number of nodes in the Service Fabric cluster. This is too limiting at times, especially in cases of stateless services. 

To circumvent this issue, the MonitorService creates another service instance of the same type to allow for more instances overall in the cluster.

This is somewhat confusing (to me at least), but the relationship terminollogy can be viewed in the following manner:
    ServiceType
        -> ServiceUri
            -> ServiceNodeInstance

For each *ServiceUri*, InstanceCount is limited by the number of nodes in the cluster. i.e. *ServiceNodeInstance* is the service instance at the node level and hence is limited by number of nodes in the cluster. 
The scaling operation will create a new *ServiceUri* from the same *ServiceType* in order to scale beyond the number of nodes.