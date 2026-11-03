namespace KongZuo
{
    // 第四周可替换样例：一排六座=观众甲，七座=空，八座=观众乙。
    // 玩家只填写6、8；7号为留座规则，永远不能放人。示例姓名不属于正式剧本设定。
    public sealed class KZSeatPuzzle
    {
        public enum Result { MissingClues, Incomplete, Incorrect, Correct }

        // 0 = 未填；1 = 观众甲；2 = 观众乙。
        private int seat6;
        private int seat8;

        public string Seat6 { get { return Name(seat6); } }
        public string Seat7 { get { return "空（不可安排观众）"; } }
        public string Seat8 { get { return Name(seat8); } }

        private static string Name(int value)
        {
            switch (value)
            {
                case 1: return "观众甲";
                case 2: return "观众乙";
                default: return "未填";
            }
        }

        public bool Cycle(int index)
        {
            if (index != 0 && index != 2) return false; // 七号位置锁定
            int current = index == 0 ? seat6 : seat8;
            int other = index == 0 ? seat8 : seat6;
            for (int step = 1; step <= 3; ++step)
            {
                int next = (current + step) % 3;
                if (next != 0 && next == other) continue; // 不允许重复使用人物卡
                if (index == 0) seat6 = next;
                else seat8 = next;
                return true;
            }
            return false;
        }

        public Result Check(bool allCluesKnown)
        {
            if (!allCluesKnown) return Result.MissingClues;
            if (seat6 == 0 || seat8 == 0) return Result.Incomplete;
            return seat6 == 1 && seat8 == 2 ? Result.Correct : Result.Incorrect;
        }
    }
}
