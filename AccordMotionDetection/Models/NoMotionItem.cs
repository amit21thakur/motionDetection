using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccordMotionDetection.Models
{
    public class NoMotionItem
    {
        public TimeSpan StartTime { get; private set; }
        public TimeSpan EndTime { get; set; }

        public NoMotionItem(TimeSpan startTime, TimeSpan endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
        public override string ToString()
        {
            return $"{StartTime.ToString(@"mm\:ss\:ffff")} -->  {EndTime.ToString(@"mm\:ss\:ffff")}  {EndTime.Subtract(StartTime).TotalSeconds}s\r\n";
        }

        public bool IsValid(int minSeconds) =>
            EndTime.Subtract(StartTime).TotalSeconds > minSeconds;
        
    }
}
