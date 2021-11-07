using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatRoom
{
    public static class ChatQueue
    {
        private static int queueCapacity = 10;
        private static List<string> _chatQueue = new List<string>(10);

        public static void Add(string message)
        {
            if (_chatQueue.Count < queueCapacity)
            {
                _chatQueue.Add(message);
            }
            else
            {
                _chatQueue.RemoveAt(0);
                _chatQueue.Add(message);
            }
        }
        public static List<string> Get()
        {
            return _chatQueue;
        }
    }
}
