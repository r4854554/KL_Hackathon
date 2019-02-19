#pragma once

//extern "C" {
//#include "apriltag.h"
//#include "tag36h11.h"
//#include "tag25h9.h"
//#include "tag16h5.h"
//#include "tagCircle21h7.h"
//#include "tagCircle49h12.h"
//#include "tagCustom48h12.h"
//#include "tagStandard41h12.h"
//#include "tagStandard52h13.h"
//}

namespace DJIVideoParser
{
	public enum class AircraftCameraType : int {
		Mavic2Pro,
		Mavic2Zoom,
		Others
	};

	public enum class AprilTagFamily : int {
		Tag16h5,
		Tag25h9,
		Tag36h11,
		TagCircle21h7,
		TagCircle49h12,
		TagCustom48h12,
		TagStandard41h12,
		TagStandard52h13
	};

	public delegate void VideoDataCallback(const Platform::Array<byte>^ data, int witdth, int height);

	public delegate void VideoFrameBufferCallback(const Platform::Array<byte>^ data, unsigned int uiWidth, unsigned int uiHeight, unsigned __int64 ulTimeStamp);

	//User should set this handler to call "DJISDKManager.Instance.VideoFeeder.ParseAssitantDecodingInfo(0, data);" inside. This would help DJIWindowsSDK to handle the image data and ask Mavic 2 for i frame and do some calibration jobs.
	public delegate Platform::Array<int>^ VideoAssistantInfoParserHandle(const Platform::Array<byte>^ data);

	public ref class AprilTagDetection sealed
	{
	public:
		property int id;
		property int hamming;
		property float decision_margin;

		/*unsigned int nrow;
		unsigned int ncol;
		double *data;*/
/*
		double c[2];
		double p[4][2];*/
	};

    public ref class Parser sealed
    {
    public:
        Parser();
		void Initialize(VideoAssistantInfoParserHandle^ handle);
		void Uninitialize(); 
		//would return RGBA image
		void SetSurfaceAndVideoCallback(int product_id, int index,  Windows::UI::Xaml::Controls::SwapChainPanel^ swap_chain_panel, VideoDataCallback^ callback);
		void PushVideoData(int product_id, int index, const Platform::Array<byte>^ data, int size);
		void SetCameraSensor(AircraftCameraType sensor);
    };
}
