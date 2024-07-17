
namespace AffectRankTraceProject
{
    /// <summary>
    /// Discrete data details
    /// </summary>
    class EventAnnotation
    {
        private long time;
        private int arousal;
        private int pleasure;
        private int dominance;
        private bool tagIsOther;
        private string tag;
        private int arousalConfRate;
        private int pleasureConfRate;
        private int dominanceConfRate;
        private int tagConfRate;

        public EventAnnotation(long time)
        {
            this.time = time;
            this.arousal = 0;
            this.pleasure = 0;
            this.dominance = 0;
            this.tagIsOther = false;
            this.tag = "";
            this.arousalConfRate = 0;
            this.pleasureConfRate = 0;
            this.dominanceConfRate = 0;
            this.tagConfRate = 0;
        }


        public long GetTime() { return this.time;}
        public void SetTime(long time) { this.time = time;}

        public int GetArousal() { return this.arousal; }
        public void SetArousal(int arousal) { this.arousal = arousal; }

        public int GetPleasure() { return this.pleasure; }
        public void SetPleasure(int pleasure) { this.pleasure = pleasure; }

        public int GetDominance() { return this.dominance; }
        public void SetDominance(int dominance) { this.dominance = dominance; }

        public string GetTag() { return this.tag; }
        public void SetTag(string tag) { this.tag = tag; }

        public bool GetTagIsOther() { return this.tagIsOther; }
        public void SetTagIsOther(bool tagIsOther) { this.tagIsOther = tagIsOther; }

        public int GetArousalConfRate() { return this.arousalConfRate; }
        public void SetArousalConfRate(int rate) { this.arousalConfRate = rate; }

        public int GetPleasureConfRate() { return this.pleasureConfRate; }
        public void SetPleasureConfRate(int rate) { this.pleasureConfRate = rate; }

        public int GetDominanceConfRate() { return this.dominanceConfRate; }
        public void SetDominanceConfRate(int rate) { this.dominanceConfRate = rate; }

        public int GetTagConfRate() { return this.tagConfRate; }
        public void SetTagConfRate(int rate) { this.tagConfRate = rate; }

        public string EventToString()
        {
            double timeInSeconds = GetTime() * 0.001;

            return timeInSeconds.ToString() + "," +
                GetArousal().ToString() + "," + GetArousalConfRate().ToString()+ "," +
                GetPleasure().ToString() + "," + GetPleasureConfRate().ToString() + "," +
                GetDominance().ToString() + "," + GetDominanceConfRate().ToString() + "," +
                GetTagIsOther().ToString() + "," + GetTag().ToString() + "," + GetTagConfRate().ToString();
        }

    }
}
