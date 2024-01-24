#if DEBUG
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SRT.Models;
using UnityEngine;
using Zenject;

namespace SRT.Managers
{
    internal class TestScoreManager : ITickable
    {
        private static readonly JsonSerializerSettings _jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        private readonly NetworkManager _networkManager;
        private readonly System.Random _random = new();

        [UsedImplicitly]
        public TestScoreManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                ScoreSubmission scoreSubmission = new()
                {
                    Index = _networkManager.Status.Index,
                    Score = _random.Next(99999)
                };
                string scoreJson = JsonConvert.SerializeObject(scoreSubmission, _jsonSerializerSettings);
                Plugin.Log.Info(scoreJson);
                _ = _networkManager.SendString(scoreJson, ServerOpcode.ScoreSubmission);
            }
        }
    }
}
#endif
