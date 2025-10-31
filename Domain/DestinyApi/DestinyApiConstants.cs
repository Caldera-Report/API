using Domain.DB;
using System.Collections.Generic;

namespace Domain.DestinyApi
{
    public static class DestinyApiConstants
    {
        public static ISet<int> NonRetryableErrorCodes { get; } = new HashSet<int>
        {
            1601, //Account not found
            1665 //Private account
        };

        public static List<OpType> OperationTypes = new List<OpType>
        {
            new OpType()
            {
                Name = "Solo Ops"
            },
            new OpType()
            {
                Name = "Fireteam Ops"
            },
            new OpType()
            {
                Name = "Pinnacle Ops"
            },
            new OpType()
            {
                Name = "Conquest"
            }
        };
    }
}
