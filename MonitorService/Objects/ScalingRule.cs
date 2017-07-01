using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitorService
{
    class ScalingRule
    {
        public long MetricThroshold;
        public MetricOperandType MetricOperand;
        public int InstaceCountChange;
        
        public enum MetricOperandType
        {
            GreaterThan,
            LessThan,
        };
    }
}
