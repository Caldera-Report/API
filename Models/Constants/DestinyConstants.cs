namespace API.Models.Constants
{
    public class DestinyConstants
    {
        public static List<uint> AllActivities = new List<uint>
        { 2489241976, 3120544689, 1948474391, 528371307, 1037546335, 167985894, 2213088605, 1604785891};

        public static List<uint> K1 = new List<uint>
            {
                528371307, 1037546335, 167985894, 2213088605, 1604785891
            };

        public const uint Caldera = 2489241976;
        public const uint Encore = 3120544689;
        public const uint KellsFall = 1948474391;

        public const uint SoloOps = 3851289711;
        public const uint FireteamOps = 556925641;
        public const uint PinnacleOps = 1227821118;

        public static List<int> MembershipTypeIds = new List<int>
        {
            1, //Xbox
            2, //PSN
            3, //Steam
            4, //Blizzard
            5, //Stadia
            6, //Egs
        };
    }
}
