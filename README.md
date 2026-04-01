# MauiSIPSorcery
跨平台远程视频方案：

玩具方案：通过各平台原生接口捕获视频帧，转化成png图片，通过WebRTC传输。未编码，带宽占用高。
WINDOWS方案：通过SIPSorceryMedia.FFmpeg库，调用FFmpeg，将视频帧用FFmpeg编码/解码，操作简单，带宽占用低，实测可行。
Android和iOS：未完待续...
