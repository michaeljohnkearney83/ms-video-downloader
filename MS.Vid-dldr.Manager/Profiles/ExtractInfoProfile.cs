using AutoMapper;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Protos.FromClient;
using MS.Vid_dldr.Manager.Dtos;

namespace MS.Vid_dldr.Manager.Profiles;

public class ExtractInfoProfile : Profile
{
    public ExtractInfoProfile()
    {
        //NOTE: Source -> Target

        //Proto <-> Entities objects
        CreateMap<ExtractInfo, RequestExtractInfoFormatsRequest>();
        CreateMap<RequestExtractInfoFormatsRequest, ExtractInfo>();

        //Entities <-> Dtos objects
    }
}