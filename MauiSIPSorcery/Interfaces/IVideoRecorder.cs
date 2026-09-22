using SIPSorceryMedia.Abstractions;
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
        event Action<uint, byte[]> OnVideoSourceEncodedSample;
        event Action<uint, int, int, byte[], VideoPixelFormatsEnum> OnVideoSourceRawSample;

        void StartRecording(IVideoEncoder encoder);
        void StopRecording();

    }

}
