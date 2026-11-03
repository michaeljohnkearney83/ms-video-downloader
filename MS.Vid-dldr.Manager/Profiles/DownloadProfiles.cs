using AutoMapper;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Protos.FromClient;
using MS.Vid_dldr.Manager.Dtos;

namespace MS.Vid_dldr.Manager.Profiles;

public class DownloadProfiles : Profile
{
    public DownloadProfiles()
    {
        //NOTE: Source -> Target

        //Proto <-> Entities objects
        CreateMap<Download, RequestDownloadRequest>();
        CreateMap<RequestDownloadRequest, Download>();

        CreateMap<Download, ExistingDownloadByIdRequest>();
        CreateMap<ExistingDownloadByIdRequest, Download>();

        //Entities <-> Dtos objects
        CreateMap<Download, DownloadRequestDto>();

        CreateMap<Download, AnalysisRequestDto>();
    }
}