﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace DonFlorito.Models
{
    public partial class Servicio
    {
        public Servicio()
        {
            ReservaServicio = new HashSet<ReservaServicio>();
            ReservasEspeciales = new HashSet<ReservasEspeciales>();
        }

        public long Id { get; set; }
        public string Nombre { get; set; }
        public long IdTipoServicio { get; set; }
        public bool IsEnabled { get; set; }

        public virtual TipoServicio IdTipoServicioNavigation { get; set; }
        public virtual ICollection<ReservaServicio> ReservaServicio { get; set; }
        public virtual ICollection<ReservasEspeciales> ReservasEspeciales { get; set; }
    }
}