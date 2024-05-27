using AutoMapper;
using DonFlorito.Models;
using Transbank.Webpay.WebpayPlus.Responses;

namespace DonFlorito.DTO
{
    public class MapperConfig : Profile
    {
        public MapperConfig()
        {
            CreateMap<Servicio, ServicioDTO>();

            CreateMap<TipoServicio, TipoServicioDTO>();

            CreateMap<PrecioServicio, PrecioServicioDTO>();

            CreateMap<Parametros,ParametrosDTO>();
            CreateMap<EstadoReserva, EstadoReservaDTO>();

            CreateMap<Persona, PersonaDTO>();
            CreateMap<OrdenCompra, OrdenCompraDTO>();

            CreateMap<Voucher, VoucherDTO>();
            CreateMap<StatusResponse, Voucher>()
                .ForMember(prop => prop.CardNumber, opt => { opt.MapFrom(x => x.CardDetail.CardNumber); });
            CreateMap<CommitResponse, Voucher>()
                .ForMember(prop => prop.CardNumber, opt => { opt.MapFrom(x => x.CardDetail.CardNumber); });

            CreateMap<Reserva, ReservaDTO>()
                .ForMember(prop => prop.Persona, opt => { opt.MapFrom(x => x.IdPersonaNavigation); })
                .ForMember(prop => prop.EstadoReserva, opt => { opt.MapFrom(x => x.IdEstadoReservaNavigation); });

            CreateMap<ReservasEspeciales, ReservaEspecialDTO>()
               .ForMember(prop => prop.Servicio, opt => { opt.MapFrom(x => x.IdServicioNavigation); })
               .ForMember(prop => prop.TipoServicio, opt => { opt.MapFrom(x => x.IdTipoServicioNavigation); });

            CreateMap<Servicio, ServicioDTO>()
                .ForMember(prop => prop.Precio, opt => { opt.MapFrom(x => x.IdTipoServicioNavigation.PrecioServicio.FirstOrDefault(p => p.IsEnabled).Precio);});

            CreateMap<ReservaServicio, ReservaServicioDTO>()
                .ForMember(prop => prop.PrecioServicio, opt => { opt.MapFrom(x => x.IdPrecioServicioNavigation); })
                .ForMember(prop => prop.Servicio, opt => { opt.MapFrom(x => x.IdServicioNavigation); });

        }
    }
}
