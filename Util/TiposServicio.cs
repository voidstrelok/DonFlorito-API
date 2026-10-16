using DonFlorito.Models;
using DonFlorito.Models.Enum;

namespace DonFlorito.Util
{
    /// <summary>Agrupaciones de tipos de servicio y reglas de temporada de los precios.</summary>
    public static class TiposServicio
    {
        public static readonly long[] Futbol = { (long)EnumTipoServicio.Futbol1, (long)EnumTipoServicio.Futbol2 };
        public static readonly long[] Futbolito = { (long)EnumTipoServicio.FutbolitoNatural, (long)EnumTipoServicio.FutbolitoSintetico };
        public static readonly long[] Tenis = { (long)EnumTipoServicio.Tenis2Personas, (long)EnumTipoServicio.Tenis4Personas };
        public static readonly long[] Piscinas = { (long)EnumTipoServicio.PiscinaGeneral, (long)EnumTipoServicio.PiscinaAM };
        public static readonly long[] Adicionales = { (long)EnumTipoServicio.ArbitroCentral, (long)EnumTipoServicio.TernaArbitral, (long)EnumTipoServicio.Balon, (long)EnumTipoServicio.Petos };

        /// <summary>Canchas con horario: se reservan por bloques y no pueden solaparse.</summary>
        public static bool EsCancha(long idTipo) => Futbol.Contains(idTipo) || Futbolito.Contains(idTipo) || Tenis.Contains(idTipo);
        public static bool EsPiscina(long idTipo) => Piscinas.Contains(idTipo);
        public static bool EsAdicional(long idTipo) => Adicionales.Contains(idTipo);

        /// <summary>Canchas de fútbol y futbolito: no abren de lunes a jueves.</summary>
        public static bool EsFutbolOFutbolito(long idTipo) => Futbol.Contains(idTipo) || Futbolito.Contains(idTipo);

        /// <summary>Tipos de cancha a los que se puede agregar el adicional (por partido).</summary>
        public static long[] CanchasDelAdicional(long idTipoAdicional) => (EnumTipoServicio)idTipoAdicional switch
        {
            EnumTipoServicio.ArbitroCentral or EnumTipoServicio.TernaArbitral => Futbol,
            EnumTipoServicio.Balon => Futbol.Concat(Futbolito).ToArray(),
            EnumTipoServicio.Petos => Futbolito,
            _ => Array.Empty<long>()
        };

        /// <summary>¿El precio rige en esa fecha? La temporada se repite cada año y puede cruzar el fin de año.</summary>
        public static bool Rige(PrecioServicio precio, DateTime fecha)
        {
            if (precio.TemporadaDesde == null || precio.TemporadaHasta == null) return true;
            var md = fecha.Month * 100 + fecha.Day;
            var desde = precio.TemporadaDesde.Value;
            var hasta = precio.TemporadaHasta.Value;
            return desde <= hasta ? md >= desde && md <= hasta : md >= desde || md <= hasta;
        }

        /// <summary>Precio habilitado que rige en la fecha (null si no hay).</summary>
        public static PrecioServicio? PrecioVigente(IEnumerable<PrecioServicio> precios, DateTime fecha)
            => precios.FirstOrDefault(p => p.IsEnabled && Rige(p, fecha));

        /// <summary>
        /// Valida las líneas de una reserva: el precio debe pertenecer al servicio y regir en la fecha reservada,
        /// y cada adicional debe acompañar una cancha que lo admita, con la misma cantidad de partidos.
        /// Devuelve el mensaje de error o null si está todo bien.
        /// </summary>
        public static string? ValidarLineas(IReadOnlyCollection<ReservaServicio> lineas, DateTime fecha)
        {
            foreach (var l in lineas)
            {
                var servicio = l.IdServicioNavigation;
                var precio = l.IdPrecioServicioNavigation;
                if (precio.IdTipoServicio != servicio.IdTipoServicio || !Rige(precio, fecha))
                {
                    return "El precio de uno o más servicios cambió o no corresponde a la fecha. Favor intente reservar nuevamente.";
                }
            }

            foreach (var a in lineas.Where(l => EsAdicional(l.IdServicioNavigation.IdTipoServicio)))
            {
                var permitidos = CanchasDelAdicional(a.IdServicioNavigation.IdTipoServicio);
                var acompana = lineas.Any(l => permitidos.Contains(l.IdServicioNavigation.IdTipoServicio) && l.Cantidad == a.Cantidad);
                if (!acompana)
                {
                    return "Los adicionales (árbitro, balón, petos) deben acompañar una cancha compatible, uno por partido.";
                }
            }
            return null;
        }
    }
}
