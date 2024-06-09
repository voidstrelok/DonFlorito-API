using DonFlorito.DTO;
using DonFlorito.Models;
using Microsoft.Extensions.Configuration;
using Transbank.Common;
using Transbank.Webpay.Common;
using Transbank.Webpay.WebpayPlus;
using Transbank.Webpay.WebpayPlus.Responses;
using QRCoder;
using SkiaSharp;
using static QRCoder.PayloadGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using SimpleSDK;
using SimpleSDK.Models.Extras;
using SimpleSDK.Enum;
using SimpleSDK.Models.DTE;
using SimpleSDK.Models.Envios;
using SimpleSDK.Helpers;
using DonFlorito.Services;
using DonFlorito.Models.Enum;
using Org.BouncyCastle.Utilities;
using SimpleSDK.Models.RegistroCompraVentas;
using Microsoft.EntityFrameworkCore;

namespace DonFlorito.Util
{
    public class Utils 
    {
        private DonFloritoContext BD { get; set; }
        private static IConfiguration Config {  get; set; }
        private static MailService Mail { get; set; }

        private readonly IWebHostEnvironment webHostEnvironment;


        public Utils(DonFloritoContext context , IConfiguration config, MailService mail, IWebHostEnvironment _webh)
        {
            BD = context;
            Config = config;
            Mail = mail;
            webHostEnvironment = _webh;
        }

        //OBTIENE LISTA DE RESERVA SEGUN FECHA, EL EVENTO INDICA FECHA COMIENZO Y FINAL DE CADA RESERVA
        public List<ContenedorEventosDTO> ObtenerEventos(List<ReservaServicio> Reservas , List<ReservasEspeciales> ReservasEsp, DateTime Fecha)
        {
            var ListaReservas = new List<ContenedorEventosDTO>();
            var param = BD.Parametros.FirstOrDefault();
            var Apertura = Fecha.AddMinutes(param.HoraApertura.TimeOfDay.TotalMinutes);
            var Cierre = Fecha.AddMinutes(param.HoraCierre.TimeOfDay.TotalMinutes);

            if (Reservas.Count > 0)
            {
                foreach (var res in Reservas)
                {
                    var Comienzo = res.HoraComienzo.Value;
                    var Final = res.HoraComienzo.Value.AddMinutes((double)(res.Cantidad * res.IdPrecioServicioNavigation.Minutos));
                    ListaReservas.Add(new ContenedorEventosDTO { HoraComienzo = Comienzo, HoraFinal = Final, Reservado = true });
                }
            }

            if (ReservasEsp.Count > 0)
            {
                foreach (var res in ReservasEsp)
                {

                    if (res.FechaComienzo.Date == Fecha.Date && res.FechaTermino.Date == Fecha.Date) // reserva es en el dia
                    {
                        ListaReservas.Add(new ContenedorEventosDTO
                        {
                            HoraComienzo = res.FechaComienzo,
                            HoraFinal = res.FechaTermino,
                            Reservado = true
                        });
                    }
                    else if (res.FechaComienzo.Date == Fecha.Date & res.FechaTermino.Date > Fecha.Date) // reserva termina despues
                    {
                        ListaReservas.Add(new ContenedorEventosDTO
                        {
                            HoraComienzo = res.FechaComienzo,
                            HoraFinal = Cierre,
                            Reservado = true
                        });
                    }
                    else if (res.FechaComienzo.Date < Fecha.Date && res.FechaTermino.Date == Fecha.Date) // reserva empieza antes
                    {
                        ListaReservas.Add(new ContenedorEventosDTO
                        {
                            HoraComienzo = Apertura,
                            HoraFinal = res.FechaTermino,
                            Reservado = true
                        });
                    }
                    else if (res.FechaComienzo.Date < Fecha.Date && res.FechaTermino.Date > Fecha.Date)// reserva antes y despues
                    {
                        ListaReservas.Add(new ContenedorEventosDTO
                        {
                            HoraComienzo = Apertura,
                            HoraFinal = Cierre,
                            Reservado = true
                        });
                    }
                }
            }
            return ListaReservas;
        }

        //COMBINA LAS RESERVAS EXISTENTES EN BLOQUES ENTEROS
        public List<ContenedorEventosDTO> AjustarCalendario(List<ContenedorEventosDTO> ListaReservas)
        {
            List<ContenedorEventosDTO> muertos = new List<ContenedorEventosDTO>();
            List<ContenedorEventosDTO> aux = new List<ContenedorEventosDTO>();

            if (ListaReservas.Count > 0)
            {
                foreach (var item in ListaReservas)
                {
                    foreach (var item2 in ListaReservas)
                    {                        
                        if (item != item2 && muertos.Count<ListaReservas.Count-1)
                        {
                            if((item.HoraFinal.TimeOfDay - item.HoraComienzo.TimeOfDay) >= (item2.HoraFinal.TimeOfDay - item2.HoraComienzo.TimeOfDay))
                            {
                                if (item2.HoraComienzo.TimeOfDay <= item.HoraFinal.TimeOfDay && item2.HoraComienzo.TimeOfDay >= item.HoraComienzo.TimeOfDay && item.HoraFinal.TimeOfDay < item2.HoraFinal.TimeOfDay)

                                {
                                    item.HoraFinal = item2.HoraFinal;
                                    muertos.Add(item2);
                                }
                                else if (item2.HoraComienzo.TimeOfDay >= item.HoraComienzo.TimeOfDay && item2.HoraFinal.TimeOfDay <= item.HoraFinal.TimeOfDay)
                                {
                                    muertos.Add(item2);
                                }
                                else if (item2.HoraComienzo.TimeOfDay == item.HoraComienzo.TimeOfDay && item2.HoraFinal.TimeOfDay == item.HoraFinal.TimeOfDay)
                                {
                                    muertos.Add(item2);
                                }
                            }    
                            
                        }
                    }
                }
                if (muertos.Count != 0)
                {
                    foreach (var item in muertos)
                    {
                        ListaReservas.Remove(item);
                    }
                    ListaReservas = AjustarCalendario(ListaReservas);
                }
                else
                {
                    return ListaReservas;
                }

            }
            else
            {
                return ListaReservas;
            }

            return ListaReservas;

        }

        //OBTIENE EL SIGUIENTE BLOQUE DISPONIBLE SEGÚN EL RANGO
        public TimeSpan SiguienteBloque(DateTime hora, int rango)
        {    

            List<TimeSpan> bloques = new List<TimeSpan>();
            var param = BD.Parametros.FirstOrDefault();
            var inicio = param.HoraApertura;
            while(inicio.TimeOfDay < hora.TimeOfDay)
            {
                inicio = inicio.AddMinutes(rango);
            }
            return inicio.TimeOfDay;

            


        }

        //NUEVA TRANSACCION TBK
        public async Task<OrdenCompra> NuevaOrdenCompra(ReservaDTO reserva)        {

            string TBKAPI = Config.GetValue<string>("TBKAPI");
            string TBKCommerce = Config.GetValue<string>("TBKCommerce");
            string FrontendUrl = Config.GetValue<string>("FrontendURL");

            OrdenCompra oc = new OrdenCompra() { };
            BD.OrdenCompra.Add(oc);
            oc.Fecha = DateTime.Now;
            await BD.SaveChangesAsync();

            string Orden = oc.Id.ToString();
            string sessionId = "DF"+ oc.Id.ToString(); //todo
            long monto = 0;
            string returnUrl = $"{FrontendUrl}/mi-reserva/";

            foreach (var item in reserva.ReservaServicio)
            {
                monto += item.PrecioServicio.Precio * item.Cantidad;
            }           

            var tr = new Transaction(new Options(TBKCommerce, TBKAPI, WebpayIntegrationType.Live));
            if (Config.GetValue<string>("Ambiente").Equals("Desarrollo"))
            {
                tr = new Transaction(new Options(IntegrationCommerceCodes.WEBPAY_PLUS, IntegrationApiKeys.WEBPAY, WebpayIntegrationType.Test));
            }
            var response = tr.Create(Orden, sessionId, monto, returnUrl);

            oc.Token = response.Token;
            oc.Url = response.Url;
            oc.IsUsed = false;
            await BD.SaveChangesAsync();

            return oc;

        }        

        //CHECK TX
        public StatusResponse Check(string token)
        {
            string TBKAPI = Config.GetValue<string>("TBKAPI");
            string TBKCommerce = Config.GetValue<string>("TBKCommerce");
            var tr = new Transaction(new Options(TBKCommerce, TBKAPI, WebpayIntegrationType.Live));

            if (Config.GetValue<string>("Ambiente").Equals("Desarrollo"))
            {
                tr = new Transaction(new Options(IntegrationCommerceCodes.WEBPAY_PLUS, IntegrationApiKeys.WEBPAY, WebpayIntegrationType.Test));
            }

            return tr.Status(token);

        }

        //COMMIT TX
        public CommitResponse Commit(string token_ws)
        {
            string TBKAPI = Config.GetValue<string>("TBKAPI");
            string TBKCommerce = Config.GetValue<string>("TBKCommerce");
            var tr = new Transaction(new Options(TBKCommerce, TBKAPI, WebpayIntegrationType.Live));

            if (Config.GetValue<string>("Ambiente").Equals("Desarrollo"))
            {
                tr = new Transaction(new Options(IntegrationCommerceCodes.WEBPAY_PLUS, IntegrationApiKeys.WEBPAY, WebpayIntegrationType.Test));
            }

            return tr.Commit(token_ws);

        }

        public byte[] GenLinkQr(long IdReserva)
        {
            Url generator = new Url(Config.GetValue<string>("FrontendURL") + "/mi-reserva/" + IdReserva.ToString());
            string payload = generator.ToString();
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);


        }

        public string Saltear(string password)
        {
            var salt = Encoding.UTF8.GetBytes(Config.GetValue<string>("salt"));
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password!,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));
            return hashed;
        }

        public string getToken(Usuario user)
        {
            var key = Encoding.UTF8.GetBytes(Config.GetValue<string>("salt"));

            var claims = new ClaimsIdentity();

            claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.IdPersonaNavigation.Nombre));

            int tiempoSesion = Config.GetValue<int>("tiempoSesion");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = claims,
                Expires = DateTime.UtcNow.AddSeconds(tiempoSesion),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenhandler = new JwtSecurityTokenHandler();
            var tokenConfig = tokenhandler.CreateToken(tokenDescriptor);

            return tokenhandler.WriteToken(tokenConfig);
        }

        #region SII 
        //public async void BoletaSII (Reserva reserva)
        //{
        //    //controlar reserva pagada en transbank

        //    Contribuyente empresa = new Contribuyente()
        //    {
        //        RazonSocial = "COMPLEJO DEPORTIVO DON FLORITO",
        //        Giro = "ACTIVIDADES DEPORTIVAS EVENTOS DEPORTIVOS ARRIENDO DE CANCHAS DEPORTIV",
        //        Direccion = "PARCELA 226",
        //        Comuna = "La Serena",
        //        FechaResolucion = new DateTime(2014, 1, 1),
        //        NumeroResolucion = 99,
        //        RutEmpresa = "76473390-8",
        //        CodigosActividades = new List<ActividadEconomica>()
        //        {
        //            new ActividadEconomica() { Codigo =  463020}, //TODO PREGUNTAR
        //        }
        //    };

        //    var Certificado = new CertificadoDigital()
        //    {
        //        Ruta = "path/to/your/certifificate.pfx", //Debe estar en formato .pfx
        //        Rut = "", 
        //        Password = ""
        //    };
        //    var UsuarioSII = new UsuarioSII()
        //    {
        //        RutUsuario = "44444444-4",
        //        PasswordSII = "123123"
        //    };
        //    var APIKey = Config.GetValue<string>("SimpleAPIKey");


        //    var emisor = new Emisor
        //    {
        //        Rut = "",
        //        GiroBoleta = "",
        //        RazonSocialBoleta = "",
        //        DireccionOrigen = "",
        //        ComunaOrigen = ""
        //    };

        //    var receptor = new SimpleSDK.Models.DTE.Receptor()
        //    {
        //        Rut = "",
        //        RazonSocial = "",
        //        Direccion = "",
        //        Comuna = ""
        //    };

        //    var tipoDte = TipoDTE.DTEType.BoletaElectronica;
        //    int folio = 123; // TODO Controlar folio somehow
        //    var dte = new DTE(emisor, receptor, folio, tipoDte);

        //    List<Detalle> detalles = new List<Detalle>();
        //    int contador = 1;
        //    foreach (var res in reserva.ReservaServicio)
        //    {
        //        var detalle = new Detalle();
        //        detalle.NumeroLinea = contador;
        //        detalle.IndicadorExento = IndicadorFacturacionExencionEnum.NotSet;
        //        //TODO Controlar detalle tal como en frontend
        //        detalle.Nombre = res.IdServicioNavigation.Nombre;
        //        detalle.Cantidad = res.Cantidad;
        //        detalle.Precio = res.IdPrecioServicioNavigation.Precio;

        //        detalle.MontoItem = (int)(res.IdPrecioServicioNavigation.Precio*res.Cantidad);
        //        detalles.Add(detalle);
        //        contador++;
        //    }

        //    dte.Documento.Detalles = detalles;
        //    dte.CalcularTotales();

        //    dte.Certificado.Ruta = Certificado.Ruta;
        //    dte.Certificado.Rut = Certificado.Rut;
        //    dte.Certificado.Password = Certificado.Password;

        //    try
        //    {
        //        var result = await dte.GenerarXMLAsync(textPathCAF.Text, handler.Configuracion.APIKey);
        //        if (result.Item1)
        //        {
        //            var pathDTE = System.IO.Path.Combine(AppContext.BaseDirectory, $"DTE_{(int)tipoDte}_{emisor.Rut}_{numericFolio.Value}.xml");
        //            System.IO.File.WriteAllText(pathDTE, result.Item2, Encoding.GetEncoding("ISO-8859-1"));
        //            MessageBox.Show($"Documento generado exitosamente y guardado en {pathDTE}", "Exito", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        }
        //        else
        //        {
        //            MessageBox.Show(result.Item2, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //        MessageBox.Show(ex.Message);
        //    }

        //    var envioSII = new SimpleSDK.Models.Envios.EnvioSII();
        //    envioSII.Ambiente = radioCertificacion.Checked ? Ambiente.AmbienteEnum.Certificacion : Ambiente.AmbienteEnum.Produccion;

        //    /*Datos del certificado*/
        //    envioSII.Certificado.Ruta = handler.Configuracion.Certificado.Ruta;
        //    envioSII.Certificado.Rut = handler.Configuracion.Certificado.Rut;
        //    envioSII.Certificado.Password = handler.Configuracion.Certificado.Password;

        //    if (comboTipoEnvio.SelectedIndex == 0) envioSII.Tipo = TipoEnvio.EnvioType.EnvioDTE;
        //    else if (comboTipoEnvio.SelectedIndex == 1) envioSII.Tipo = TipoEnvio.EnvioType.EnvioBoleta;
        //    else if (comboTipoEnvio.SelectedIndex == 2) envioSII.Tipo = TipoEnvio.EnvioType.RVD;
        //    else if (comboTipoEnvio.SelectedIndex == 3) envioSII.Tipo = TipoEnvio.EnvioType.LVC;

        //    OpenFileDialog openFileDialog = new OpenFileDialog();
        //    openFileDialog.Multiselect = false;
        //    openFileDialog.Title = $"Seleccione archivo para ser enviado al SII";
        //    openFileDialog.Filter = $"Archivo XML (*.xml)|*.xml";
        //    if (openFileDialog.ShowDialog() == DialogResult.OK)
        //    {
        //        string pathFile = openFileDialog.FileName;
        //        var result = await envioSII.EnviarSIIAsync(pathFile, handler.Configuracion.APIKey);
        //        if (envioSII.Tipo == TipoEnvio.EnvioType.EnvioBoleta)
        //        {
        //            ResultadoOperacion formulario = new ResultadoOperacion(result.Item2.ToString());
        //            formulario.ShowDialog();
        //        }
        //        else
        //        {
        //            ResultadoOperacion formulario = new ResultadoOperacion(result.Item2.ResponseXml);
        //            formulario.ShowDialog();
        //        }

        //    }
        //}
        #endregion

        public async void EnviarCorreoReservaCancelada(Reserva Reserva)
        {
            var Cliente = Reserva.IdPersonaNavigation;

            var DestinatarioCliente = new List<string>
            {
                Cliente.Email,
            };

            var BodyCliente = string.Empty;
            using (var reader = new StreamReader(webHostEnvironment.ContentRootPath + "/Resources/Email/EmailCancelarReservaCliente.html"))
            {
                BodyCliente = reader.ReadToEnd();
            }

            BodyCliente = BodyCliente.Replace("{fecha}", Reserva.FechaReserva.ToShortDateString());
            BodyCliente = BodyCliente.Replace("{nombre}", Cliente.Nombre + " " + Cliente.ApellidoPaterno);
            BodyCliente = BodyCliente.Replace("{id}", Reserva.Id.ToString());

            var Asunto = "Reserva DF" + Reserva.Id.ToString() + " Cancelada";
            if (Config.GetValue<string>("Ambiente").Equals("Desarrollo"))
            {
                Asunto = "*DEMO* " + Asunto;
            }
            Mail.Send(DestinatarioCliente, Asunto, BodyCliente, null);
        }


        public async void EnviarCorreoReservaPagada(Reserva Reserva, Voucher vc)
        {

            var Cliente = Reserva.IdPersonaNavigation;


            var DestinatarioCliente = new List<string>
            {
                Cliente.Email,
            };
            var DestinatarioAdmin = new List<string>
            {
                Config.GetValue<string>("MailAdmin"),
            };

            var BodyCliente = string.Empty;
            using (var reader = new StreamReader(webHostEnvironment.ContentRootPath + "/Resources/Email/EmailPagoReservaCliente.html"))
            {
                BodyCliente = reader.ReadToEnd();
            }
            var BodyAdmin = string.Empty;
            using (var reader = new StreamReader(webHostEnvironment.ContentRootPath + "/Resources/Email/EmailPagoReservaAdmin.html"))
            {
                BodyAdmin = reader.ReadToEnd();
            }

            var detalleServicios = "";
            foreach (var servicio in Reserva.ReservaServicio)
            {
                if (servicio.IdServicioNavigation.IdTipoServicio == (long)EnumTipoServicio.Quincho)
                {
                    detalleServicios += $"<tr class='servicio'><td class='text-end'><div><strong>{servicio.Cantidad.ToString()} x {servicio.IdServicioNavigation.Nombre}</strong></div></td><td class='text-start'>${servicio.IdPrecioServicioNavigation.Precio * servicio.Cantidad}</td></tr>";
                }
                else if (servicio.IdServicioNavigation.IdTipoServicio == (long)EnumTipoServicio.PiscinaGeneral || servicio.IdServicioNavigation.IdTipoServicio == (long)EnumTipoServicio.PiscinaAM)
                {
                    detalleServicios += $"<tr class='servicio'><td class='text-end'><div><strong>{servicio.Cantidad.ToString()} x Entrada {servicio.IdServicioNavigation.Nombre}</strong></div></td><td class='text-start'>${servicio.IdPrecioServicioNavigation.Precio * servicio.Cantidad}</td></tr>";

                }
                else
                {
                    detalleServicios += $"<tr class='servicio'><td class='text-end'><div><strong>{servicio.Cantidad.ToString()} Partido - {servicio.IdServicioNavigation.Nombre}</strong><br>Desde {servicio.HoraComienzo.Value.ToShortTimeString()} - {servicio.IdPrecioServicioNavigation.Minutos*servicio.Cantidad} min.</div></td><td class='text-start'>${servicio.IdPrecioServicioNavigation.Precio * servicio.Cantidad}</td></tr>";

                }
            }

            var detalleVoucher = "";
            var tipoPago = "";
            switch (vc.PaymentTypeCode)
            {
                case "VD":
                    tipoPago = "Venta Débito";
                    break;
                case "VN":
                    tipoPago = "Venta Normal";
                    break;
                case "VC":
                    tipoPago = "Venta en cuotas";
                    break;
                case "SI":
                    tipoPago = "3 cuotas sin interés";
                    break;
                case "S2":
                    tipoPago = "2 cuotas sin interés";
                    break;
                case "NC":
                    tipoPago = vc.InstallmentsNumber.ToString() + " cuotas sin interés";
                    break;
                case "VP":
                    tipoPago = "Venta Prepago";
                    break;
            }



            detalleVoucher += $"<tr><td class='text-end'><strong>Detalle Tarjeta</strong></td><td class='text-start'>Tarjeta terminada en {vc.CardNumber}</td></tr>";
            detalleVoucher += $"<tr><td class='text-end'><strong>Tipo de pago</strong></td><td class='text-start'>{tipoPago}</td></tr>";
            if (!vc.PaymentTypeCode.Equals("VD") && !vc.PaymentTypeCode.Equals("VN") && !vc.PaymentTypeCode.Equals("VP"))
            {
                detalleVoucher += $"<tr><td class='text-end'><strong>Coutas</strong></td><td class='text-start'>{vc.InstallmentsNumber} cuotas de {vc.InstallmentsAmount}</td></tr>";
            }
            detalleVoucher += $"<tr><td class='text-end'><strong>Total</strong></td><td class='text-start'>${vc.Amount}</td></tr>";
            detalleVoucher += $"<tr><td class='text-end'><strong>Fecha autorización</strong></td><td class='text-start'>{DateTime.Parse(vc.TransactionDate)}</td></tr>";

            var detallevoucherAdmin = $"<tr><td class='text-end'><strong>Cliente</strong></td><td class='text-start'>{Cliente.Nombre} {Cliente.ApellidoPaterno} {Cliente.ApellidoMaterno}</td></tr>";
            detallevoucherAdmin += $"<tr><td class='text-end'><strong>Rut</strong></td><td class='text-start'>{Cliente.Rut}</td></tr>";
            detallevoucherAdmin += $"<tr><td class='text-end'><strong>Teléfono</strong></td><td class='text-start'>{Cliente.Telefono}</td></tr>";
            detallevoucherAdmin += $"<tr><td class='text-end'><strong>Email</strong></td><td class='text-start'>{Cliente.Email}</td></tr>";
            detallevoucherAdmin += detalleVoucher;

            BodyCliente = BodyCliente.Replace("{detalle-voucher}", detalleVoucher);
            BodyCliente = BodyCliente.Replace("{fecha}", Reserva.FechaReserva.ToShortDateString());
            BodyCliente = BodyCliente.Replace("{detalle-reserva}", detalleServicios);
            BodyCliente = BodyCliente.Replace("{nombre}", Cliente.Nombre + " " + Cliente.ApellidoPaterno);
            BodyCliente = BodyCliente.Replace("{id}", Reserva.Id.ToString());

            BodyAdmin = BodyAdmin.Replace("{detalle-voucher}", detallevoucherAdmin);
            BodyAdmin = BodyAdmin.Replace("{fecha}", Reserva.FechaReserva.ToShortDateString());
            BodyAdmin = BodyAdmin.Replace("{detalle-reserva}", detalleServicios);
            BodyAdmin = BodyAdmin.Replace("{id}", Reserva.Id.ToString());

            var QR = GenLinkQr(Reserva.Id);

            var Asunto = "Reserva DF" + Reserva.Id.ToString() + " Confirmada";
            if (Config.GetValue<string>("Ambiente").Equals("Desarrollo"))
            {
                Asunto = "*DEMO* " + Asunto;
            }
            Mail.Send(DestinatarioCliente, Asunto, BodyCliente, QR);
            Mail.Send(DestinatarioAdmin, Asunto, BodyAdmin, QR);

        }
    }


}
