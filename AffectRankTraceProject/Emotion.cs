using System;

namespace AffectRankTraceProject
{
    class Emotion
    {
        public string tag;
        public double[] value = new double[3];

        public Emotion(string rowData)
        {
            string[] data = rowData.Split(',');

            this.tag = data[0];
            this.value[0] = Convert.ToDouble(data[1]);
            this.value[1] = Convert.ToDouble(data[2]);
            this.value[2] = Convert.ToDouble(data[3]);
        }
    }
}
