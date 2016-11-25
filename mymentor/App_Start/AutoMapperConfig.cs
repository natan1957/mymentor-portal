using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AutoMapper;
using MyMentor.BL.Dto;
using MyMentor.BL.Models;
using Parse;

namespace MyMentor.App_Start
{
    public static class AutoMapperConfig
    {
        public static void RegisterMappings()
        {
            MapCoupon();
        }

        private static void MapCoupon()
        {
            Mapper.CreateMap<Coupon, CouponDto>()
                .ForMember(x => x.BundleId, x => x.MapFrom(z => z.Bundle.ObjectId))
                .ForMember(x => x.CurrencyId, x => x.MapFrom(z => z.Currency.ObjectId))
                .ForMember(x => x.IssueEventId, x => x.MapFrom(z => z.IssueEvent.ObjectId))
                .ForMember(x => x.IssuedById, x => x.MapFrom(z => z.IssuedBy.ObjectId))
                .ForMember(x => x.IssuedForId, x => x.MapFrom(z => z.IssuedFor.ObjectId))
                .ForMember(x => x.UseEventId, x => x.MapFrom(z => z.UseEvent.ObjectId));

            Mapper.CreateMap<CouponDto, Coupon>()
                .ForMember(x => x.Bundle, x => x.MapFrom(z => ParseObject.CreateWithoutData<Bundle>(z.BundleId)))
                .ForMember(x => x.Currency, x => x.MapFrom(z => ParseObject.CreateWithoutData<Currency>(z.CurrencyId)))
                .ForMember(x => x.IssueEvent, x => x.MapFrom(z => ParseObject.CreateWithoutData<Event>(z.IssueEventId)))
                .ForMember(x => x.IssuedBy, x => x.MapFrom(z => ParseObject.CreateWithoutData<ParseUser>(z.IssuedById)))
                .ForMember(x => x.IssuedFor, x => x.MapFrom(z => ParseObject.CreateWithoutData<ParseUser>(z.IssuedForId)))
                .ForMember(x => x.UseEvent, x => x.MapFrom(z => ParseObject.CreateWithoutData<Event>(z.UseEventId)));
        }
    }
}