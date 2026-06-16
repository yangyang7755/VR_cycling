using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Microsoft.CognitiveServices.Speech;

namespace PainLabDeviceVoiceRecognitionAzure
{
    class ResultDataFrame
    {
        public int recognition_val;
    }

    class CommandDataFrame
    {
        public string recognition_command;
    }

    class PainLabDeviceAzureSpeechRatingsProtocol : PainlabProtocol
    {
        static string descriptorPath = "Resources/device-descriptor.json";
        SpeechConfig config = null;
        SpeechRecognizer recognizer = null;
        Dictionary<String, int> numDict = new Dictionary<string, int>()
        {
            { "Ten", 10 },
            { "Zero", 0 },
            { "One", 1 },
            { "Two", 2 },
            { "Three", 3 },
            { "Four", 4 },
            { "Five", 5 },
            { "Six", 6 },
            { "Seven", 7 },
            { "Eight", 8 },
            { "Nine", 9 }
        };
        protected override void RegisterWithDescriptor()
        {
            string descriptorString = File.ReadAllText(descriptorPath);
            SendString(descriptorString);

            return;
        }

        public void ControlApplicationThread()
        {
            while (true)
            {
                _waitOnControlSem.WaitOne();
                HandlingControlData();
            }
        }

        public void setupRecognizer()
        {
            config = SpeechConfig.FromSubscription(AzureServicesCred.subscriptionKey, AzureServicesCred.serviceRegion);

            recognizer = new SpeechRecognizer(config);

            var phraseList = PhraseListGrammar.FromRecognizer(recognizer);
            phraseList.Clear();
            foreach (var item in numDict)
            {
                phraseList.AddPhrase(item.Key);
                phraseList.AddPhrase(item.Value.ToString());
            }

            recognizer.Recognizing += (s, e) => {
                Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                recognizeResultHandling(e.Result.Text);
            };

            // No need for full recognition for just one word. wired things happen
            //recognizer.Recognized += (s, e) => {
            //    var result = e.Result;
            //    Console.WriteLine($"Reason: {result.Reason.ToString()}");
            //    if (result.Reason == ResultReason.RecognizedSpeech)
            //    {
            //        recognizeResultHandling(result.Text);
            //    }
            //};

            recognizer.Canceled += (s, e) => {
                Console.WriteLine($"\n    Canceled. Reason: {e.Reason.ToString()}, CanceledReason: {e.Reason}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you update the subscription info?");
            };

            recognizer.SessionStarted += (s, e) => {
                Console.WriteLine("\n    Session started event.");
            };

            recognizer.SessionStopped += (s, e) => {
                Console.WriteLine("\n    Session stopped event.");
            };
        }

        private int getResult(string res)
        {
            int ret = -1;
            foreach (var item in numDict)
            {
                if (res.Contains(item.Key) ||
                    res.Contains(item.Value.ToString()))
                {
                    return item.Value;
                }
            }
            return ret;
        }

        private void recognizeResultHandling(string res)
        {
            int recognitionValue = getResult(res);
            if (recognitionValue != -1)
            {
                ResultDataFrame dataFrame = new ResultDataFrame();
                dataFrame.recognition_val = recognitionValue;
                Console.WriteLine("Sending result: " + recognitionValue.ToString());

                byte[] byteData = StringToBytes(JsonConvert.SerializeObject(dataFrame, Formatting.None));
                UpdateFrameData(byteData);
            }
        }

        private async Task RunRecognizerCommand(string cmd)
        {
            if (cmd == "one")
            {
                await recognizer.RecognizeOnceAsync();
                Console.WriteLine("Recognising a single speech");
            }
            else if (cmd == "continuous")
            {
                await recognizer.StartContinuousRecognitionAsync();
                Console.WriteLine("Running in continuous mode");
            }
            else if (cmd == "stop")
            {
                await recognizer.StopContinuousRecognitionAsync();
                Console.WriteLine("Recognition Stopped");
            }

        }

        protected override void ApplyControlData()
        {
            CommandDataFrame controlFrame
                    = JsonConvert.DeserializeObject<CommandDataFrame>
                      (Encoding.UTF8.GetString(_controlBuffer, 0, (int)_numControlBytes));

            Task runCmd = Task.Run(async () => await RunRecognizerCommand(controlFrame.recognition_command));
        }
    }

    class Program
    {
        static string networkConfigPath = "Resources/network-config.json";

        static void Main(string[] args)
        {
            PainLabDeviceAzureSpeechRatingsProtocol protocol = new PainLabDeviceAzureSpeechRatingsProtocol();

            string networkJsonString = File.ReadAllText(networkConfigPath);
            NetworkConfig netConf = JsonConvert.DeserializeObject<NetworkConfig>(networkJsonString);

            protocol.Init(netConf, waitOnControl: true);
            protocol.setupRecognizer();

            Thread controlThread = new Thread(new ThreadStart(protocol.ControlApplicationThread));
            controlThread.Start();
            Console.WriteLine("Speech recognition setup complete");

            Console.WriteLine("Waiting for command...");
            string waitInput = Console.ReadLine();
        }
    }
}
