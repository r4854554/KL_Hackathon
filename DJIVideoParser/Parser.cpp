#include "pch.h"
#include "Parser.h"
#include "modulemediator.h"

#include <Windows.Storage.h>

using namespace DJIVideoParser;
using namespace Platform;


using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Core;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Automation::Peers;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Controls::Primitives;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::Xaml::Navigation;
using namespace Windows::UI::Xaml::Interop;
using namespace concurrency;
using namespace Platform;
using namespace Windows::Storage;

using namespace dji::videoparser;

namespace DJIVideoParser {
	//dji::videoparser::ModuleMediator* g_pModuleMediator = new dji::videoparser::ModuleMediator;
}
Parser::Parser()
{
}

// wstring => string
static std::string WString2String(const std::wstring& ws)
{
	auto wideData = ws.c_str();
	int bufferSize = WideCharToMultiByte(CP_ACP, 0, wideData, -1, nullptr, 0, NULL, NULL);
	auto targetData = std::make_unique<char[]>(bufferSize);
	if (0 == WideCharToMultiByte(CP_ACP, 0, wideData, -1, targetData.get(), bufferSize, NULL, NULL))
		throw std::exception("Can't convert string to ACP");

	return std::string(targetData.get());
}

void DJIVideoParser::Parser::Initialize(VideoAssistantInfoParserHandle^ handle)
{
	auto folder = Windows::Storage::ApplicationData::Current->LocalFolder->Path;

	auto wstring = std::wstring(folder->Begin());
	std::string path = WString2String(wstring);
	g_pModuleMediator->Initialize(path, [handle](uint8_t* data, int length)
	{
		if (handle)
		{
			Platform::Array<unsigned char>^ out = nullptr;
			//DecodingAssistInfo^ info = nullptr;
			auto info = handle(ref new Platform::Array<unsigned char>(data, length));
			
			DJIDecodingAssistInfo res = { 0 };
			if (info->Length >= 7)
			{
				res.has_lut_idx = info[0];
				res.has_time_stamp = info[1];
				res.should_ignore = info[2];
				res.force_30_fps = info[3];
				res.lut_idx = info[4];
				res.fov_state = info[5];
				res.timestamp = info[6];
			}
			return res;
		}
	});
}

void DJIVideoParser::Parser::Uninitialize()
{
	g_pModuleMediator->Uninitialize(); 
}

void DJIVideoParser::Parser::SetSurfaceAndVideoCallback(int product_id, int index, Windows::UI::Xaml::Controls::SwapChainPanel^ swap_chain_panel, VideoDataCallback^ callback)
{
	std::shared_ptr<dji::videoparser::VideoParserMgr> video_parser_mgr = g_pModuleMediator->GetVideoParserMgr().lock();

	if (video_parser_mgr)
	{
		if (callback == nullptr)
		{
			video_parser_mgr->SetWindow(product_id, index, nullptr, swap_chain_panel);
		}
		else
		{
			video_parser_mgr->SetWindow(product_id, index, [callback](uint8_t *data, int width, int height) {
				callback(Platform::ArrayReference<byte>(data, width * height * 4), width, height);
			}, swap_chain_panel);
		}
	}
}

void DJIVideoParser::Parser::PushVideoData(int product_id, int index, const Platform::Array<byte>^ data, int size)
{
	std::shared_ptr<dji::videoparser::VideoParserMgr> video_parser_mgr = g_pModuleMediator->GetVideoParserMgr().lock();

	if (video_parser_mgr)
	{
		//                LOGD << "video_parser_mgr->ParserData ";
		video_parser_mgr->ParserData(product_id, index, data->Data, size);
	}
}


void DJIVideoParser::Parser::SetCameraSensor(AircraftCameraType sensor)
{
	std::shared_ptr<dji::videoparser::VideoParserMgr> video_parser_mgr = g_pModuleMediator->GetVideoParserMgr().lock();

	static std::map<AircraftCameraType, DeviceCameraSensor> map = 
	{
		{ AircraftCameraType::Mavic2Pro, DeviceCameraSensor::imx283 },
		{ AircraftCameraType::Mavic2Zoom, DeviceCameraSensor::imx477 },
		{ AircraftCameraType::Others, DeviceCameraSensor::Unknown },
	};
	
	video_parser_mgr->SetSensor(map[sensor]);
}

Platform::Array<AprilTagDetection^>^ DJIVideoParser::Parser::DetectAprilTag(const Platform::Array<byte>^ data, int width, int height, AprilTagFamily tagFamily)
{
	float decimate = 1.0;	
	float blur = 0.8;
	int refineEdges = 0;
	int threadNum = 1;
	apriltag_family_t *tf = NULL;

	switch (tagFamily)
	{
	case AprilTagFamily::Tag16h5:
		tf = tag16h5_create();
		break;
	case AprilTagFamily::Tag25h9:
		tf = tag25h9_create();
		break;
	case AprilTagFamily::Tag36h11:
		tf = tag36h11_create();
		break;
	default:
		tf = tag36h11_create();
		break;
	}

	apriltag_detector_t *td = apriltag_detector_create();
	apriltag_detector_add_family(td, tf);

	td->quad_decimate = decimate;
	td->quad_sigma = blur;
	td->nthreads = threadNum;
	td->debug = 0;
	td->refine_edges = refineEdges;

	image_u8_t im = {
		width,		// .width
		height,		// .height
		width * 3,	// .stride
		data->Data	// .buf
	};

	zarray_t *detections = apriltag_detector_detect(td, &im);

	Array<AprilTagDetection^>^ rtnDetections = ref new Array<AprilTagDetection^>(zarray_size(detections));

	for (int i = 0; i < zarray_size(detections); i++) {
		apriltag_detection_t *d = NULL;
		zarray_get_volatile(detections, i, d);
		
		DJIVideoParser::AprilTagDetection^ rtnD = ref new AprilTagDetection();

		rtnD->id = d->id;
		rtnD->hamming = d->hamming;
		rtnD->decision_margin = d->decision_margin;

		rtnDetections[i] = rtnD;
	}

	zarray_destroy(detections);
	apriltag_detector_destroy(td);
	
	switch (tagFamily)
	{
	case AprilTagFamily::Tag16h5:
		tag16h5_destroy(tf);
		break;
	case AprilTagFamily::Tag25h9:
		tag25h9_destroy(tf);
		break;
	case AprilTagFamily::Tag36h11:
		tag36h11_destroy(tf);
		break;
	}

	return rtnDetections;
}

image_u8_t *DJIVideoParser::Parser::image_u8_from_u8x4(image_u8x4_t *src)
{
	int stride = src->width * 3;
	uint8_t *buf = (uint8_t*)malloc(src->height * stride * sizeof(uint8_t));

	for (int y = 0; y < src->height; y++)
		for (int x = 0; x < src->width; x++)
		{
			buf[y * stride + x + 0] = src->buf[y * src->stride + x + 0];
			buf[y * stride + x + 1] = src->buf[y * src->stride + x + 1];
			buf[y * stride + x + 2] = src->buf[y * src->stride + x + 2];
		}

	image_u8_t tmp = {
		src->width,
		src->height,
		stride,
		buf
	};
	image_u8_t *copy = (image_u8_t*)calloc(1, sizeof(image_u8_t));
	memcpy(copy, &tmp, sizeof(image_u8_t));

	return copy;
}