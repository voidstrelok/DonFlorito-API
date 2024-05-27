using AutoMapper;
using DonFlorito.DTO;
using DonFlorito.Models;
using DonFlorito.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DonFlorito.Controllers
{
    [ApiController]
    [Route("api/personas")]
    public class PersonasController : ControllerBase
    {

        private readonly ILogger<SessionController> _logger;
        private readonly DonFloritoContext BD;
        private readonly IMapper Mapper;

        public PersonasController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
        }

        [AllowAnonymous]
        [Route("GetPersonaByRut")]
        [HttpPost]
        public PersonaDTO GetPersonaByRut([FromForm] string RUT)        
        {
            Persona Persona = BD.Persona.Where(p=>p.Rut == RUT && p.IsEnabled).FirstOrDefault();
            if(Persona != null) 
            {
                PersonaDTO PersonaDTO = Mapper.Map<PersonaDTO>(Persona);
                return PersonaDTO;

            }
            else
            {
                return null;
            }
        }

        [Authorize]
        [Route("getPersonas")]
        [HttpGet]
        public List<PersonaDTO> getPersonas()
        {
            var Personas = BD.Persona.ToList();

            List<PersonaDTO> PersonasDTO = Personas.Select(p => Mapper.Map<PersonaDTO>(p)).ToList();

            return PersonasDTO;
        }

    }
}