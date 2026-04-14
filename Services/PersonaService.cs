using DonFlorito.DTO;
using DonFlorito.Models;
using Microsoft.AspNetCore.Mvc;
using MimeKit;

namespace DonFlorito.Services
{
    public class PersonaService
    {
        private readonly DonFloritoContext BD;

        public PersonaService(DonFloritoContext context)
        {
            BD = context;
        }

        public async Task<(long IdPersona, IActionResult? Error)> ObtenerOCrearPersonaAsync(long? idPersona, PersonaCreacionDTO? personaCreacion)
        {
            if (idPersona != null)
            {
                var persona = BD.Persona.FirstOrDefault(p => p.Id == idPersona);
                if (persona == null)
                {
                    return (0, new BadRequestObjectResult("La persona no existe."));
                }

                return (persona.Id, null);
            }

            if (personaCreacion == null)
            {
                return (0, new BadRequestObjectResult("Sin datos de persona."));
            }

            var personaExistente = BD.Persona.FirstOrDefault(p => p.Rut == personaCreacion.Rut);
            if (personaExistente != null)
            {
                return (personaExistente.Id, null);
            }

            var rutValido = (new Rut.Rut(personaCreacion.Rut)).IsValid;
            if (!rutValido)
            {
                return (0, new BadRequestObjectResult("RUT no válido"));
            }

            if (!MailboxAddress.TryParse(personaCreacion.Email, out _))
            {
                return (0, new BadRequestObjectResult("Email no válido"));
            }

            var nuevaPersona = new Persona()
            {
                Nombre = personaCreacion.Nombre,
                SegundoNombre = personaCreacion.SegundoNombre,
                ApellidoPaterno = personaCreacion.ApellidoPaterno,
                ApellidoMaterno = personaCreacion.ApellidoMaterno,
                Email = personaCreacion.Email,
                Telefono = personaCreacion.Telefono,
                Rut = personaCreacion.Rut,
                IsEnabled = true,
            };

            BD.Persona.Add(nuevaPersona);
            await BD.SaveChangesAsync();
            return (nuevaPersona.Id, null);
        }
    }
}
