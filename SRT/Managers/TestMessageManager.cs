#if DEBUG
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace SRT.Managers
{
    internal class TestMessageManager : ITickable
    {
        private readonly MessageManager _messageManager;
        private readonly System.Random _random = new();
        private readonly string[] _testMessages =
        {
            "Hello, World!",
            "undefined",
            "!@#$%^&*()`~",
            "The cake is a lie",
            "Terrible terrible damage",
            "C#",
            "Wow what a cool event!",
            "Cyan is a furry",
            "I come to cleanse this land",
            "ITS NO USE!",
            "we are back",
            "what is a beat saber? a miserable pile of cubes",
            "NUCLEAR LAUNCH DETECTED",
            "beat saber. beat saber never changes",
            "get pwned n00b",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua",
            "hey guys, i think we should all use audiolink",
            "mawntee is a furry",
            "owo whats this?",
            "The right man in the wrong place can make all the difference in the world",
            "All your base are belong to us",
            "I used to be a player like you, then I took an arrow in the knee",
            "Press alt+f4 for free robux",
            "Entaro adun",
            "|||||||||||",
            "This entire discord server must be purged.",
            "She sells seashells by the sea shore.",
            "Connection terminated.",
            "LEEEEEEEEERRRRRROOOOOOOOYYYYYYYYYYYYY",
            "blame reaxt",
            "vivify is kewl",
            "Why did i spend time writing these?",
            "good morning cyan",
            "har har har har",
            "TWENTY EIGHT EXCEPTIONS",
            "Stay a while and listen",
            "Did I miss anything?",
            "I see much of myself in you, and I can tell you from personal experience that things do indeed get better.",
            "Resources.FindObjectOfTypeAll<Player>().ToList().ForEach(n => n.GiveHug());"
        };

        private bool _testActive;
        private float _timer;

        [UsedImplicitly]
        public TestMessageManager(MessageManager messageManager)
        {
            _messageManager = messageManager;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                _testActive = !_testActive;
            }

            if (!_testActive)
            {
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0)
            {
                return;
            }

            _messageManager.SendMessage(_testMessages[_random.Next(_testMessages.Length)]);
            _timer = 1;
        }
    }
}
#endif
