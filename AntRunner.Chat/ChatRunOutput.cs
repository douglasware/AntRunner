using AntRunnerLib;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntRunner.Chat
{
    public class ChatRunOutput : ThreadRunOutput
    {
        public List<Message>? Messages { get; set; }
    }
}
