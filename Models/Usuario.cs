﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace DonFlorito.Models
{
    public partial class Usuario
    {
        public long Id { get; set; }
        public string Usuario1 { get; set; }
        public string Contraseña { get; set; }
        public long IdPersona { get; set; }
        public bool IsEnabled { get; set; }

        public virtual Persona IdPersonaNavigation { get; set; }
    }
}