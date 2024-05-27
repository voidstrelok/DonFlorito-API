using AutoMapper;
using DonFlorito.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DonFlorito.Util;
using System.Transactions;
using DonFlorito.Models.Enum;
using Microsoft.EntityFrameworkCore;
using DonFlorito.DTO;
namespace DonFlorito.Controllers
{
    [ApiController]
    [Route("api/transacciones")]
    public class TransaccionesController : ControllerBase
    {

        private readonly ILogger<SessionController> _logger;
        private readonly DonFloritoContext BD;
        private readonly IMapper Mapper;
        private readonly Utils Util;

        public TransaccionesController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils utils)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = utils;
        }

        [AllowAnonymous]
        [Route("commit")]
        [HttpPost]
        public async Task<ActionResult> Commit([FromForm] string token_ws)
        {
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {

                try
                {
                        var respuesta = Util.Commit(token_ws);

                        Voucher vc = Mapper.Map<Voucher>(respuesta);

                        OrdenCompra orden = BD.OrdenCompra.Where(t => t.Id == long.Parse(vc.BuyOrder)).Include(t => t.IdReservaNavigation).FirstOrDefault();

                        Reserva reserva = BD.Reserva.Where(r => r.Id == orden.IdReserva)
                            .Include(r => r.IdEstadoReservaNavigation)
                            .Include(r => r.IdPersonaNavigation)
                            .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation).ThenInclude(rs => rs.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                            .Include(r => r.OrdenCompra).ThenInclude(oc => oc.Voucher)
                            .FirstOrDefault();

                        List<OrdenCompra> ordenes = reserva.OrdenCompra.ToList();

                        switch (vc.ResponseCode)
                        {
                            case 0:
                                //crear reserva
                                reserva.IdEstadoReserva = (long)EnumEstadoReserva.Confirmada;
                                reserva.FechaConfirmacion = DateTime.Now;
                                Util.EnviarCorreoReservaPagada(reserva,vc);
                                break;
                            default:
                                // fallo del pago
                                reserva.IdEstadoReserva = (long)EnumEstadoReserva.Anulada;
                                reserva.IsEnabled = false;                     
                                break;
                        }

                        vc.IdOrdenCompra = orden.Id;
                        vc.Fecha = DateTime.Now;

                        BD.Voucher.Add(vc);
                        BD.SaveChanges();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
                await BD.SaveChangesAsync();
                transactionScope.Complete();
                return Ok();
            }

        }

        [AllowAnonymous]
        [Route("usaEnlace")]
        [HttpPost]
        public async Task<ActionResult<ReservaDTO>> usaEnlace([FromBody] ReservaDTO reserva)
        {

            if(reserva == null)
            {
                return NotFound("Sin reserva");
            }

            var OrdenCompra = reserva.OrdenCompra.OrderByDescending(o=>o.Fecha).FirstOrDefault();
            if(OrdenCompra != null)
            {
                OrdenCompra.IsUsed = true;
            }

            if (reserva.OrdenCompra.Count >= 5){
                reserva.IdEstadoReserva = (long)EnumEstadoReserva.Anulada;
            }
            else
            {
                OrdenCompraDTO OrdenDTO = Mapper.Map<OrdenCompraDTO>(await Util.NuevaOrdenCompra(reserva));
                reserva.OrdenCompra.Add(OrdenDTO);
                reserva.IdOrdenCompra = OrdenDTO.Id;


            }
            await BD.SaveChangesAsync();
            reserva.OrdenCompra = reserva.OrdenCompra.OrderByDescending(o => o.Fecha).ToList();
            return reserva;
        }

       
    }
}