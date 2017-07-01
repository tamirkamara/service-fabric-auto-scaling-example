using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitorService
{
    class ServiceScalingPlan
    {
        public string ServiceTypeName;
        public List<ScalingRule> ScalingRules;
        public int MinInstanceCount;
        public int MaxInstanceCount;
    }
}
