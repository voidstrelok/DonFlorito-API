﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace DonFlorito.Models
{
    public partial class TipoServicio
    {
        public TipoServicio()
        {
            PrecioServicio = new HashSet<PrecioServicio>();
            ReservasEspeciales = new HashSet<ReservasEspeciales>();
            Servicio = new HashSet<Servicio>();
        }

        public long Id { get; set; }
        public string Nombre { get; set; }

        public virtual ICollection<PrecioServicio> PrecioServicio { get; set; }
        public virtual ICollection<ReservasEspeciales> ReservasEspeciales { get; set; }
        public virtual ICollection<Servicio> Servicio { get; set; }
    }
}