using System.Collections.Generic;

namespace NHQTools.FileFormats.Pff
{
    public class PffGameInfo
    {

        public string GameName { get; private set; }
        public string GameNameExt { get; private set; }
        public string PropertyName { get; private set; }
        public uint VersionNumber { get; private set; }
        public uint EntryRecordLength { get; private set; }

        ////////////////////////////////////////////////////////////////////////////////////
        public PffGameInfo(string gameName, string propertyName, uint versionNumber, uint entryRecordLength)
        {
            GameName = gameName;
            GameNameExt = $"{gameName} (PFF{versionNumber}-{entryRecordLength})";
            PropertyName = propertyName;
            VersionNumber = versionNumber;
            EntryRecordLength = entryRecordLength;

            PffGames.All.Add(this);
        }

    }

    ////////////////////////////////////////////////////////////////////////////////////
    public static class PffGames
    {
        public static List<PffGameInfo> All { get; } = new List<PffGameInfo>();

        ////////////////////////////////////////////////////////////////////////////////////
        // Pff0_32
        public static PffGameInfo F22Raptor { get; private set; } = new PffGameInfo("F-22 Raptor", nameof(F22Raptor), 0, 32);

        ////////////////////////////////////////////////////////////////////////////////////
        // Pff2_32
        public static PffGameInfo Comanche3Gold { get; private set; } = new PffGameInfo("Comanche 3 Gold", nameof(Comanche3Gold), 2, 32);

        ////////////////////////////////////////////////////////////////////////////////////
        // Pff3_32
        public static PffGameInfo ArmoredFist { get; private set; } = new PffGameInfo("Armored Fist", nameof(ArmoredFist), 3, 32);
        public static PffGameInfo DeltaForce { get; private set; } = new PffGameInfo("Delta Force", nameof(DeltaForce), 3, 32);
        public static PffGameInfo DeltaForce2 { get; private set; } = new PffGameInfo("Delta Force 2", nameof(DeltaForce2), 3, 32);
        public static PffGameInfo F16MultiRoleFighter { get; private set; } = new PffGameInfo("F-16 Multirole Fighter", nameof(F16MultiRoleFighter), 3, 32);
        public static PffGameInfo F22Lightning3 { get; private set; } = new PffGameInfo("F-22 Lightning 3", nameof(F22Lightning3), 3, 32);
        public static PffGameInfo F22RaptorIbs { get; private set; } = new PffGameInfo("F-22 Raptor IBS", nameof(F22RaptorIbs), 3, 32);
        public static PffGameInfo Mig29Fulcrum { get; private set; } = new PffGameInfo("MiG-29 Fulcrum", nameof(Mig29Fulcrum), 3, 32);

        ////////////////////////////////////////////////////////////////////////////////////
        // Pff3_36
        public static PffGameInfo BlackHawkDown { get; private set; } = new PffGameInfo("Delta Force: Black Hawk Down", nameof(BlackHawkDown), 3, 36);
        public static PffGameInfo Comanche4 { get; private set; } = new PffGameInfo("Comanche 4", nameof(Comanche4), 3, 36);
        public static PffGameInfo DeltaForceXtreme { get; private set; } = new PffGameInfo("Delta Force: Xtreme", nameof(DeltaForceXtreme), 3, 36);
        public static PffGameInfo JointOperations { get; private set; } = new PffGameInfo("Joint Operations: Typhoon Rising", nameof(JointOperations), 3, 36);
        public static PffGameInfo LandWarrior { get; private set; } = new PffGameInfo("Delta Force: Land Warrior", nameof(LandWarrior), 3, 36);
        public static PffGameInfo Tachyon { get; private set; } = new PffGameInfo("Tachyon: The Fringe", nameof(Tachyon), 3, 36);
        public static PffGameInfo TaskForceDagger { get; private set; } = new PffGameInfo("Delta Force: Task Force Dagger", nameof(TaskForceDagger), 3, 36);

        ////////////////////////////////////////////////////////////////////////////////////
        // Pff4_36
        public static PffGameInfo DeltaForceXtreme2 { get; private set; } = new PffGameInfo("Delta Force: Xtreme 2", nameof(DeltaForceXtreme2), 4, 36);

    }

}