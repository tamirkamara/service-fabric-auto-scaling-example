# Service Fabric Auto Scaling Service #

When using Service Fabric to host your services, it’s sometimes required to scale them according to the overall load. 
Currently ServiceFabric supports setting the scale to a fixed number, or to define the service to exist on every node in the cluster which is somewhat flexible since if there’ll be a change in the cluster size the service will be scaled accordingly. 
However, if you need to scale dynamically according to some metric, then you’ll have to implement something on your own. There’s an option to use PowerShell scripts to do that, or like in the following example, have a service in ServiceFabric in charge of scaling other services in an automatic manner. 

The solution is comprised of two services:
* `MonitorService` - this is the actual auto scaling service
* `Stateless1` - a getting started service that would be scaled. 

When running this example on your local 5 node cluster, the Stateless service would start with 4 instances and be scaled to 7 (1 at a time), and then be scaled back to only 1 instance. 

The MonitorService is using a dummy metric when deciding whether to scale in/out, that value is coming from the `Utils.GetMetricValue` method. 
It also has some preconfigured scaling rules that are defined in the `Utils.GetServiceScalingPlan` method.
Both these methods are simple and were created to make it easy to test the auto scaling functionality. Naturally, that you’d want to change the functionally in and/or around those methods to accommodate real world scenarios.


### HyperScaling ###
The number of instances allowed for each service (aka InstanceCount) is limited to number of nodes in the Service Fabric cluster. This is too limiting at times, especially in cases of stateless services. 

To circumvent this issue, the MonitorService creates another service instance of the same type to allow for more instances overall in the cluster.

This is somewhat confusing (to me at least), but the relationship terminology can be viewed in the following manner:
    ServiceType
        -> ServiceUri
            -> ServiceNodeInstance

For each *ServiceUri*, InstanceCount is limited by the number of nodes in the cluster. i.e. *ServiceNodeInstance* is the service instance at the node level and hence is limited by number of nodes in the cluster. 
The scaling operation will create a new *ServiceUri* from the same *ServiceType* in order to scale beyond the number of nodes.
