namespace Domain.DestinyApi
{
    public class PostGameCarnageReportData
    {
        public DateTime period { get; set; }
        public int startingPhaseIndex { get; set; }
        public bool activityWasStartedFromBeginning { get; set; }
        public int activityDifficultyTier { get; set; }
        public long[] selectedSkullHashes { get; set; }
        public Activitydetails activityDetails { get; set; }
        public Entry[] entries { get; set; }
        public object[] teams { get; set; }
    }

    public class Activitydetails
    {
        public long referenceId { get; set; }
        public long directorActivityHash { get; set; }
        public string instanceId { get; set; }
        public int mode { get; set; }
        public int[] modes { get; set; }
        public bool isPrivate { get; set; }
        public int membershipType { get; set; }
    }

    public class Entry
    {
        public int standing { get; set; }
        public Score score { get; set; }
        public PGCRPlayer player { get; set; }
        public string characterId { get; set; }
        public Values values { get; set; }
        public Extended extended { get; set; }
    }

    public class Score
    {
        public Basic basic { get; set; }
    }

    public class Basic
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class PGCRPlayer
    {
        public Destinyuserinfo destinyUserInfo { get; set; }
        public string characterClass { get; set; }
        public long classHash { get; set; }
        public long raceHash { get; set; }
        public long genderHash { get; set; }
        public int characterLevel { get; set; }
        public int lightLevel { get; set; }
        public long emblemHash { get; set; }
    }

    public class Destinyuserinfo
    {
        public string iconPath { get; set; }
        public int crossSaveOverride { get; set; }
        public int[] applicableMembershipTypes { get; set; }
        public bool isPublic { get; set; }
        public int membershipType { get; set; }
        public string membershipId { get; set; }
        public string displayName { get; set; }
        public string bungieGlobalDisplayName { get; set; }
        public int bungieGlobalDisplayNameCode { get; set; }
    }

    public class Values
    {
        public Assists assists { get; set; }
        public Completed completed { get; set; }
        public Deaths deaths { get; set; }
        public Kills kills { get; set; }
        public Opponentsdefeated opponentsDefeated { get; set; }
        public Efficiency efficiency { get; set; }
        public Killsdeathsratio killsDeathsRatio { get; set; }
        public Killsdeathsassists killsDeathsAssists { get; set; }
        public Score1 score { get; set; }
        public Activitydurationseconds activityDurationSeconds { get; set; }
        public Completionreason completionReason { get; set; }
        public Fireteamid fireteamId { get; set; }
        public Startseconds startSeconds { get; set; }
        public Timeplayedseconds timePlayedSeconds { get; set; }
        public Playercount playerCount { get; set; }
        public Teamscore teamScore { get; set; }
    }

    public class Assists
    {
        public Basic1 basic { get; set; }
    }

    public class Basic1
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Completed
    {
        public Basic2 basic { get; set; }
    }

    public class Basic2
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Deaths
    {
        public Basic3 basic { get; set; }
    }

    public class Basic3
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Kills
    {
        public Basic4 basic { get; set; }
    }

    public class Basic4
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Opponentsdefeated
    {
        public Basic5 basic { get; set; }
    }

    public class Basic5
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Efficiency
    {
        public Basic6 basic { get; set; }
    }

    public class Basic6
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Killsdeathsratio
    {
        public Basic7 basic { get; set; }
    }

    public class Basic7
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Killsdeathsassists
    {
        public Basic8 basic { get; set; }
    }

    public class Basic8
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Score1
    {
        public Basic9 basic { get; set; }
    }

    public class Basic9
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Activitydurationseconds
    {
        public Basic10 basic { get; set; }
    }

    public class Basic10
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Completionreason
    {
        public Basic11 basic { get; set; }
    }

    public class Basic11
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Fireteamid
    {
        public Basic12 basic { get; set; }
    }

    public class Basic12
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Startseconds
    {
        public Basic13 basic { get; set; }
    }

    public class Basic13
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Timeplayedseconds
    {
        public Basic14 basic { get; set; }
    }

    public class Basic14
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Playercount
    {
        public Basic15 basic { get; set; }
    }

    public class Basic15
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Teamscore
    {
        public Basic16 basic { get; set; }
    }

    public class Basic16
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Extended
    {
        public Weapon[] weapons { get; set; }
        public Values1 values { get; set; }
        public Scoreboardvalues scoreboardValues { get; set; }
    }

    public class Values1
    {
        public Precisionkills precisionKills { get; set; }
        public Weaponkillsgrenade weaponKillsGrenade { get; set; }
        public Weaponkillsmelee weaponKillsMelee { get; set; }
        public Weaponkillssuper weaponKillsSuper { get; set; }
        public Weaponkillsability weaponKillsAbility { get; set; }
    }

    public class Precisionkills
    {
        public Basic17 basic { get; set; }
    }

    public class Basic17
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Weaponkillsgrenade
    {
        public Basic18 basic { get; set; }
    }

    public class Basic18
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Weaponkillsmelee
    {
        public Basic19 basic { get; set; }
    }

    public class Basic19
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Weaponkillssuper
    {
        public Basic20 basic { get; set; }
    }

    public class Basic20
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Weaponkillsability
    {
        public Basic21 basic { get; set; }
    }

    public class Basic21
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Scoreboardvalues
    {
        public Assists1 assists { get; set; }
        public Deaths1 deaths { get; set; }
        public Kills1 kills { get; set; }
        public Score2 score { get; set; }
        public Partial_Score partial_score { get; set; }
        public Display_Total_Multiplier display_total_multiplier { get; set; }
        public Display_Team_Multiplier display_team_multiplier { get; set; }
        public Display_Time_Bonus_Points display_time_bonus_points { get; set; }
        public Performance_Grade_Multiplier performance_grade_multiplier { get; set; }
    }

    public class Assists1
    {
        public Basic22 basic { get; set; }
    }

    public class Basic22
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Deaths1
    {
        public Basic23 basic { get; set; }
    }

    public class Basic23
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Kills1
    {
        public Basic24 basic { get; set; }
    }

    public class Basic24
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Score2
    {
        public Basic25 basic { get; set; }
    }

    public class Basic25
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Partial_Score
    {
        public Basic26 basic { get; set; }
    }

    public class Basic26
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Display_Total_Multiplier
    {
        public Basic27 basic { get; set; }
    }

    public class Basic27
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Display_Team_Multiplier
    {
        public Basic28 basic { get; set; }
    }

    public class Basic28
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Display_Time_Bonus_Points
    {
        public Basic29 basic { get; set; }
    }

    public class Basic29
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Performance_Grade_Multiplier
    {
        public Basic30 basic { get; set; }
    }

    public class Basic30
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Weapon
    {
        public long referenceId { get; set; }
        public Values2 values { get; set; }
    }

    public class Values2
    {
        public Uniqueweaponkills uniqueWeaponKills { get; set; }
        public Uniqueweaponprecisionkills uniqueWeaponPrecisionKills { get; set; }
        public Uniqueweaponkillsprecisionkills uniqueWeaponKillsPrecisionKills { get; set; }
    }

    public class Uniqueweaponkills
    {
        public Basic31 basic { get; set; }
    }

    public class Basic31
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Uniqueweaponprecisionkills
    {
        public Basic32 basic { get; set; }
    }

    public class Basic32
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

    public class Uniqueweaponkillsprecisionkills
    {
        public Basic33 basic { get; set; }
    }

    public class Basic33
    {
        public float value { get; set; }
        public string displayValue { get; set; }
    }

}