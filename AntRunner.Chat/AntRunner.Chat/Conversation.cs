using OpenAI;
using OpenAI.Chat;

namespace AntRunner.Chat
{
    public class Conversation
    {
        private readonly ChatRunOptions _chatConfiguration;
        private readonly AzureOpenAiConfig _serviceConfiguration;
        private readonly Guid _conversationId = Guid.NewGuid();
        private List<Message> _messages = new List<Message>();

        public Conversation(ChatRunOptions chatConfiguration) : this(chatConfiguration, AzureOpenAiConfigFactory.Get()) { }

        public Conversation(ChatRunOptions chatConfiguration, AzureOpenAiConfig serviceConfiguration)
        {
            _chatConfiguration = chatConfiguration;
            _serviceConfiguration = serviceConfiguration;
        }

        public Guid ConversationId { get { return _conversationId; } }

        public async Task<ChatRunOutput> Chat(string newInstructions)
        {
            // Update the instructions before running the thread
            _chatConfiguration.Instructions = newInstructions;

            var runnerOutput = await ChatRunner.RunThread(_chatConfiguration, _serviceConfiguration, _messages);

            // Update messages with the latest ones from the output
            _messages = runnerOutput?.Messages!;

            return runnerOutput!;
        }

        public void Undo()
        {
            int lastIndex = _messages.FindLastIndex(m => m.Role == Role.User);
            if (lastIndex != -1)
            {
                _messages.RemoveRange(lastIndex, _messages.Count - lastIndex);
            }
        }

        public void Undo(string messageText)
        {
            int lastIndex = _messages.FindLastIndex(m => m.Role == Role.User && m.Content == (dynamic)messageText);
            if (lastIndex != -1)
            {
                _messages.RemoveRange(lastIndex, _messages.Count - lastIndex);
            }
        }
    }
}