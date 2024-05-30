using AutoMapper;
using DonFlorito.DTO;
using DonFlorito.Models;
using DonFlorito;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonFlorito.Models.Enum;
using System.Collections.Generic;
using DonFlorito.Util;
using Microsoft.AspNetCore.HostFiltering;
using System.Globalization;
namespace DonFlorito.Controllers
{
    [ApiController]
    [Route("api/servicios")]
    public class ServiciosController : ControllerBase
    {
        
        private readonly ILogger<SessionController> _logger;
        private readonly DonFloritoContext BD;
        private readonly IMapper Mapper;
        private readonly Utils Util;
        
        public ServiciosController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils utils)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = utils;
        }

        [AllowAnonymous]
        [Route("getCatalogo")]
        [HttpPost]
        public List<TipoServicioDTO> getCatalogo()
        {
            var TipoServicios = BD.TipoServicio
                .Include(ts=>ts.Servicio.Where(s=>s.IsEnabled))
                .Include(ts=>ts.PrecioServicio.Where(p=>p.IsEnabled))
                .OrderBy(ts=>ts.Id)
                .ToList();

            List<TipoServicioDTO> TipoServiciosDTO = TipoServicios.Select(ts => Mapper.Map<TipoServicioDTO>(ts)).ToList();
            TipoServiciosDTO = TipoServiciosDTO.Where(ts=>ts.Servicio.Count > 0).ToList();
            return TipoServiciosDTO;
        }

        [Authorize]
        [Route("getServicios")]
        [HttpGet]
        public List<ServicioDTO> getServicios()
        {
            var Servicios = BD.Servicio.ToList();

            List<ServicioDTO> ServiciosDTO = Servicios.Select(s => Mapper.Map<ServicioDTO>(s)).ToList();

            return ServiciosDTO;
        }

        [Authorize]
        [Route("getAllTipoServicios")]
        [HttpGet]
        public List<TipoServicioDTO> getAllTipoServicios()
        {
            var Servicios = BD.TipoServicio.Include(ts=>ts.Servicio).ToList();

            List<TipoServicioDTO> ServiciosDTO = Servicios.Select(s => Mapper.Map<TipoServicioDTO>(s)).ToList();

            return ServiciosDTO;
        }

        [AllowAnonymous]
        [Route("getCalendarioByServicio")]
        [HttpPost]
        public List<ContenedorEventosDTO> getCalendarioByServicio([FromForm] long IdServicio,[FromForm] long nPartidos, [FromForm] string FechaReserva)
        {
            var Fecha = DateTime.ParseExact(FechaReserva, "dd-MM-yyyy", CultureInfo.InvariantCulture);

            var Servicio = BD.Servicio.Find(IdServicio);
            var ListaReservas = new List<ContenedorEventosDTO>();

            var param = BD.Parametros.FirstOrDefault();
            var Apertura = Fecha.AddMinutes(param.HoraApertura.TimeOfDay.TotalMinutes);
            var Cierre = Fecha.AddMinutes(param.HoraCierre.TimeOfDay.TotalMinutes);


            var MinutosAbierto = (int)((param.HoraCierre.TimeOfDay - param.HoraApertura.TimeOfDay).TotalMinutes);

            if (Servicio.IdTipoServicio != (long)EnumTipoServicio.Quincho && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaGeneral && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaAM)
            {
                var reservas = BD.ReservaServicio.Where(rs => rs.IdServicio == Servicio.Id && rs.IdReservaNavigation.FechaReserva.Date == Fecha && rs.IdReservaNavigation.IsEnabled && rs.IdReservaNavigation.IdEstadoReserva == (long)EnumEstadoReserva.Confirmada).Include(r=>r.IdPrecioServicioNavigation)
                    .OrderBy(rs => rs.HoraComienzo)
                    .ToList();
                var reservasEsp = BD.ReservasEspeciales.Where(re => (re.FechaComienzo.Date <= Fecha || re.FechaTermino >= Fecha) && ( re.IsRecinto || re.IdServicio == Servicio.Id) && re.IsEnabled).ToList();

                ListaReservas = Util.ObtenerEventos(reservas, reservasEsp, Fecha);

                //agregar hora actual
                //TODO Determinar si puede reservar hoy
                var hoy = DateTime.Now;
                if (hoy.Date == Fecha.Date && hoy.TimeOfDay > Apertura.TimeOfDay)
                {
                    ListaReservas.Add(new ContenedorEventosDTO
                    {
                        HoraComienzo = Apertura,
                        HoraFinal = hoy,
                        Reservado = true
                    });
                }
                //TODO concatenar fechas para mostrar bien boni, controlar click en fecha reservada
                ListaReservas.OrderBy(a => a.HoraComienzo).ToList();


                ListaReservas = Util.AjustarCalendario(ListaReservas);

                ListaReservas=ListaReservas.OrderBy(a=>a.HoraComienzo).ToList();

                //agregar rangos disponibles
                var inicio = Apertura;
                PrecioServicio PrecioServicio = BD.PrecioServicio.Where(p => p.IsEnabled && p.IdTipoServicio == Servicio.IdTipoServicio).FirstOrDefault();
                int rango = (int)(nPartidos * PrecioServicio.Minutos);
                List<ContenedorEventosDTO> aux = new List<ContenedorEventosDTO>();
                
                if(ListaReservas.Count > 1)
                {
                    foreach (var item in ListaReservas)
                    {
                        var minutos = (int)(item.HoraComienzo.TimeOfDay - inicio.TimeOfDay).TotalMinutes;
                        if (minutos >= rango)
                        {
                            for (int i = 0; i <= minutos; i += (int)rango)
                            {
                                if ((minutos - i) >= rango)
                                {
                                    aux.Add(new ContenedorEventosDTO()
                                    {
                                        HoraComienzo = inicio.AddMinutes((double)i),
                                        HoraFinal = inicio.AddMinutes((double)(i + rango)),
                                        Reservado = false
                                    });
                                }
                            }
                        }
                        
                        inicio = inicio.Date.Add(Util.SiguienteBloque(item.HoraFinal,rango));

                    }
                }
                else if (ListaReservas.Count == 1)
                {
                    var item = ListaReservas[0];
                    inicio = inicio.Date.Add(Util.SiguienteBloque(item.HoraFinal, rango));

                    var minutos = (int)(Cierre.TimeOfDay - Apertura.TimeOfDay).TotalMinutes;
                    if (minutos >= rango)
                    {
                        for (int i = 0; i <= minutos; i += (int)rango)
                        {
                            if ((minutos - i) >= rango)
                            {
                                var hinicio = inicio.AddMinutes((double)i);
                                var hfinal = inicio.AddMinutes((double)(i + rango));
                                //Evento entre evento no disponible
                                if (!(hinicio.TimeOfDay >= item.HoraComienzo.TimeOfDay && hinicio.TimeOfDay < item.HoraFinal.TimeOfDay) &&
                                    !(hinicio.TimeOfDay < item.HoraComienzo.TimeOfDay && hfinal.TimeOfDay > item.HoraComienzo.TimeOfDay) &&
                                    !(hinicio.TimeOfDay >= item.HoraComienzo.TimeOfDay && hfinal.TimeOfDay <= item.HoraFinal.TimeOfDay) &&
                                    !(hfinal.TimeOfDay > Cierre.TimeOfDay))
                                {
                                    aux.Add(new ContenedorEventosDTO()
                                    {
                                        HoraComienzo = hinicio,
                                        HoraFinal = hfinal,
                                        Reservado = false
                                    });
                                }
                                
                            }
                        }
                    }
                }
                else
                {
                    var minutos = (int)(Cierre.TimeOfDay - Apertura.TimeOfDay).TotalMinutes;
                    if (minutos >= rango)
                    {
                        for (int i = 0; i <= minutos; i += (int)rango)
                        {
                            if ((minutos - i) >= rango)
                            {
                                var hinicio = inicio.AddMinutes((double)i);
                                var hfinal = inicio.AddMinutes((double)(i + rango));
                                aux.Add(new ContenedorEventosDTO()
                                {
                                    HoraComienzo = hinicio,
                                    HoraFinal = hfinal,
                                    Reservado = false
                                });
                            }
                        }
                    }
                }
                

                ListaReservas.AddRange(aux);
            }

            return ListaReservas;
        }

        [AllowAnonymous]
        [Route("GetTipoServicioById")]
        [HttpPost]
        public TipoServicioDTO GetTipoServicioById([FromForm] long IdTipoServicio)
        {
            List<Servicio> Servicios = BD.Servicio.Where(s => s.IdTipoServicio == IdTipoServicio && s.IsEnabled)
                .Include(s=>s.IdTipoServicioNavigation.PrecioServicio)
                .ToList();
            var ts = Servicios.Select(s=>s.IdTipoServicioNavigation).FirstOrDefault();

            TipoServicioDTO TsDTO = Mapper.Map<TipoServicioDTO>(ts);
            return TsDTO;
        }


        
    }
}