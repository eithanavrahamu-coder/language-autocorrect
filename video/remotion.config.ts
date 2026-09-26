import { Config } from '@remotion/cli/config';

// Sharp text: every frame is taken as a lossless picture, then encoded as a high-quality H.264 MP4 that plays
// everywhere (phones, WhatsApp, YouTube, social sites).
Config.setVideoImageFormat('png');
Config.setCodec('h264');
Config.setCrf(16);
Config.setX264Preset('slow');
Config.setPixelFormat('yuv420p');
Config.setColorSpace('bt709');
Config.setAudioBitrate('320k');
Config.setOverwriteOutput(true);
