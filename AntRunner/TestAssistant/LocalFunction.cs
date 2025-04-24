using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAssistant
{
    public class LocalFunction
    {
        public static string GetDateAndTime()
        {
            return DateTime.Now.ToString("F");
        }

        public static int AddNumbers(int a, int b)
        {
            return a + b;
        }
    }
}
