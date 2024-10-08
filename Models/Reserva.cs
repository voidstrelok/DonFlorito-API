﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace DonFlorito.Models
{
    public partial class Reserva
    {
        public Reserva()
        {
            OrdenCompra = new HashSet<OrdenCompra>();
            ReservaServicio = new HashSet<ReservaServicio>();
        }

        public long Id { get; set; }
        public long IdEstadoReserva { get; set; }
        public long IdPersona { get; set; }
        public DateTime FechaReserva { get; set; }
        public DateTime FechaIngreso { get; set; }
        public DateTime? FechaConfirmacion { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public string Comentario { get; set; }
        public bool IsEnabled { get; set; }

        public virtual EstadoReserva IdEstadoReservaNavigation { get; set; }
        public virtual Persona IdPersonaNavigation { get; set; }
        public virtual ICollection<OrdenCompra> OrdenCompra { get; set; }
        public virtual ICollection<ReservaServicio> ReservaServicio { get; set; }
    }
}