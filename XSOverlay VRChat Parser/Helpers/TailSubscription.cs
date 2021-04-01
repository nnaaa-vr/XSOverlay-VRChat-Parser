using System;
using System.IO;
using System.Text;
using System.Threading;

namespace XSOverlay_VRChat_Parser.Helpers
{
    public class TailSubscription : IDisposable
    {
        private string _filePath { get; set; }
        public delegate void OnUpdate(string content);
        private OnUpdate updateFunc { get; set; }
        private Timer timer { get; set; }
        private long lastSize { get; set; }

        public TailSubscription(string filePath, OnUpdate func, long dueTimeMilliseconds, long frequencyMilliseconds)
        {
            _filePath = filePath;
            updateFunc = func;
            lastSize = new FileInfo(filePath).Length;
            timer = new Timer(new TimerCallback(ExecOnUpdate), null, dueTimeMilliseconds, frequencyMilliseconds);
        }

        private void ExecOnUpdate(object timerState)
        {
            if (!File.Exists(_filePath))
            {
                Dispose();
                return;
            }

            long size = new FileInfo(_filePath).Length;

            if (size > lastSize)
            {
                using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        sr.BaseStream.Seek(lastSize, SeekOrigin.Begin);

                        StringBuilder outputContent = new StringBuilder();
                        string line = string.Empty;

                        while ((line = sr.ReadLine()) != null)
                            outputContent.Append(line + "\n");

                        lastSize = sr.BaseStream.Position;

                        updateFunc(outputContent.ToString());
                    }
                }
            }
        }

        public void Dispose()
        {
            timer.Dispose();
            updateFunc = null;
        }
    }
}
