using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MauiSIPSorcery.Interfaces
{
    public interface IVideoRecorder
    {
        event Action<byte[]> OnVideoFrameArrived;

        void StartRecording();
        void StopRecording();

    }

}
