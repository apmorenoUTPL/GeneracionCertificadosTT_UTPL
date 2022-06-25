using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Security.Cryptography;
using System.Text;
using Certificados.Models;
using Certificados.LogHistorial;
using QRCoder;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Kernel.Geom;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Image;
using System.IO;

namespace Certificados.Controllers
{
    //[Authorize]
    public class ComerciantesController : Controller
    {
        private readonly ComerciantesEntities dbContextComerciantes = new ComerciantesEntities();
        public Log log = new Log();

        public ComerciantesEntities DbContextComerciantes => dbContextComerciantes;

        // GET: Comerciantes
        public ActionResult Index()
        {
            return View("NuevaBusqueda");
        }

        public ActionResult NuevaBusqueda()
        {
            return View();
        }

        public ActionResult BuscarCertificado()
        {
            return View("NuevaBusqueda");
        }

        [HttpPost]
        public ActionResult BuscarCertificado(string cedulaString, string apellidosString)  // buscar Comerciante en BDD por 
        {
            try
            {
                // texto ingresada para búsqueda
                ViewBag.DataCedula = cedulaString;
                ViewBag.DataApellidos = apellidosString;
                ViewBag.CertificadoGenerado = false;
                ViewBag.RectificacionGenerado = false;
                ViewBag.RectificacionId = 0;

                // validación de extensión de cedulaString (10 caracteres)
                if (cedulaString.Length == 10 && !String.IsNullOrWhiteSpace(apellidosString))
                {
                    // remover espacios blancos al inicio y final del string
                    apellidosString = apellidosString.Trim();

                    // comprobar si tiene certificado generado
                    Comerciantes comercianteTemp = GetComercianteByCedulaApellidos(cedulaString, apellidosString);

                    if (comercianteTemp != null)
                    {
                        if (GetCertificadoByComerId(comercianteTemp.Id) != null)
                        {
                            ViewBag.CertificadoGenerado = true;
                        }
                        else
                        {
                            // comprobar si tiene rectificación generada
                            Rectificaciones rectTemp = GetRectificacionByComerId(comercianteTemp.Id);
                            if (rectTemp != null)
                            {
                                ViewBag.RectificacionGenerado = true;
                                ViewBag.RectificacionId = rectTemp.Id;
                            }
                        }

                        log.Info(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Coincidencia encontrada de comerciante",
                            usuario_log = "Externo con parámetros " + cedulaString + " y apellidos " + apellidosString
                        });
                        return View(comercianteTemp);
                    }
                    else
                    {
                        log.Info(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No existen coincidencias de comerciante",
                            usuario_log = "Externo con parámetros " + cedulaString + " y apellidos " + apellidosString
                        });
                        return View("NoResultado");
                    }
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Ingresa cédula con menos de 10 caracteres",
                        usuario_log = "Externo con parámetros " + cedulaString + " y apellidos " + apellidosString
                    });
                    return View("NoResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al buscar de comerciante",
                    usuario_log = "Externo con parámetros " + cedulaString + " y apellidos " + apellidosString,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                return View("NuevaBusqueda");
            }
        }


        public ActionResult GenerarCC(Comerciantes comerciante)    // generar Certificado PDF para descargar
        {
            try
            {
                Models.Certificados certTemp = GetCertificadoByComerId(comerciante.Id);
                comerciante = GetComercianteById(comerciante.Id);
                string pathDestinoDocumento = Server.MapPath("~/Documentos/CC/");

                // verificar si ya generó el documento para recuperarlo, caso contrario se registra
                if (certTemp != null)
                {
                    // si se ha generado, abrir documento y enviar para descarga
                    byte[] fileBytes = System.IO.File.ReadAllBytes(pathDestinoDocumento + certTemp.ruta_archivo);
                    return File(fileBytes, "application/pdf", certTemp.ruta_archivo);
                }
                else
                {
                    // verificar que exista una plantilla cargada para generar el CC
                    PlantillasCC plantillaCC = GetArchivoPlantillaCCActiva();
                    if (plantillaCC != null)
                    {
                        // DOCUMENTO: generar pdf y registrar objeto
                        // plantilla certificado path
                        string pathArchivoPlantilla = "~/PlantillasCC/";
                        string nombreArchivoPlantilla = plantillaCC.archivo_plantilla;

                        // si no se ha generado, crear el documento, registrar y enviar para descarga
                        certTemp = new Models.Certificados()
                        {
                            fecha_emision = DateTime.Now,
                            num_certificado = GetNumeroCertificado(),
                            comerciantes_id = comerciante.Id,
                            codigo_verificacion = CreateHashString(comerciante.Cedula),
                            ruta_archivo = comerciante.Apellidos.Trim().Replace(" ", "_") + "_" + DateTime.Now.GetHashCode().ToString().Replace("-", "") + ".pdf",
                            certificado_valido = true,
                            plantillascc_id = plantillaCC.Id
                        };

                        if (!String.IsNullOrWhiteSpace(nombreArchivoPlantilla))
                        {
                            // abrir plantilla
                            var pdfReader = new PdfReader(Request.MapPath(pathArchivoPlantilla + nombreArchivoPlantilla));

                            // buffer para almacenar
                            MemoryStream ms = new MemoryStream();
                            PdfWriter pw = new PdfWriter(ms);
                            PdfDocument pdf = new PdfDocument(pdfReader, pw);
                            Document doc = new Document(pdf, PageSize.A4);

                            // mapeo de datos - campos obligatorios
                            PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                            IDictionary<String, PdfFormField> fields = form.GetFormFields();

                            fields.TryGetValue("numero_doc", out PdfFormField toSet);
                            toSet.SetValue(GetNumBaseCertificado() + DateTime.Today.Year + "-" + certTemp.num_certificado);
                            fields.TryGetValue("fecha_doc", out toSet);
                            toSet.SetValue(certTemp.fecha_emision.ToLongDateString());
                            fields.TryGetValue("nombres_apellidos", out toSet);
                            toSet.SetValue(comerciante.Nombres.ToString().ToUpper() + " " + comerciante.Apellidos.ToString().ToUpper());
                            fields.TryGetValue("cedula", out toSet);
                            toSet.SetValue(comerciante.Cedula);
                            fields.TryGetValue("capacitacion", out toSet);
                            toSet.SetValue(comerciante.Capacitacion.ToString());
                            fields.TryGetValue("anio_cc", out toSet);
                            toSet.SetValue(certTemp.fecha_emision.Year.ToString());
                            fields.TryGetValue("institucion", out toSet);
                            toSet.SetValue(comerciante.Institucion2.nombre_institucion);
                            fields.TryGetValue("directorDCA", out toSet);
                            toSet.SetValue(GetDirectorDCA());
                            form.FlattenFields();

                            // generar y agregar código QR
                            ImageData imageData = ImageDataFactory.CreatePng(GenerarQRByte(comerciante.Nombres.Trim().Replace(" ", "_") + comerciante.Cedula));
                            iText.Layout.Element.Image image = new iText.Layout.Element.Image(imageData);
                            var pageWidth = pdf.GetDefaultPageSize().GetWidth();
                            var pageHeight = pdf.GetDefaultPageSize().GetHeight();
                            image.SetHeight(140f);
                            image.SetWidth(140f);
                            image.SetFixedPosition(pageWidth - 140f, 20f);
                            doc.Add(image);
                            doc.Close();

                            // guardar bytes
                            byte[] byteStream = ms.ToArray();
                            ms = new MemoryStream();
                            ms.Write(byteStream, 0, byteStream.Length);
                            ms.Position = 0;

                            // guardar en servidor
                            FileStream fileStream = new FileStream(pathDestinoDocumento + certTemp.ruta_archivo, FileMode.Create, FileAccess.ReadWrite);
                            ms.WriteTo(fileStream);
                            fileStream.Close();

                            // registrar en Tabla Certificados
                            dbContextComerciantes.Certificados.Add(certTemp);
                            if (dbContextComerciantes.SaveChanges() > 0)
                            {
                                log.Transaction(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera CC y registra en DB exitosamente",
                                    usuario_log = "Usuario externo " + comerciante.Cedula + " y " + comerciante.Apellidos
                                });
                                return View(GetCertificadoById(certTemp.Id));
                            }
                            else
                            {
                                log.Warning(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se guardó en base de datos el objeto certificado",
                                    usuario_log = "Usuario externo " + comerciante.Cedula + " y " + comerciante.Apellidos
                                });

                                BorrarArchivo(pathDestinoDocumento + certTemp.ruta_archivo);
                                ViewBag.Mensaje = "Ha ocurrido un problema al momento de generar el documento. Por favor inténtelo más tarde.";
                                return View("ProblemaGenerar");
                            }
                        }
                        else
                        {
                            log.Warning(new Log_Registros()
                            {
                                mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Existe un problema con la plantilla CC",
                                usuario_log = "Usuario externo " + comerciante.Cedula + " y " + comerciante.Apellidos
                            });

                            ViewBag.Mensaje = "Ha ocurrido un problema al momento de generar el documento. Por favor inténtelo más tarde.";
                            return View("ProblemaGenerar");
                        }
                    }
                    else
                    {
                        // no se carga aún la plantilla de CC
                        log.Warning(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra plantilla CC para generar",
                            usuario_log = "Usuario externo " + comerciante.Cedula + " y " + comerciante.Apellidos
                        });

                        ViewBag.Mensaje = "Actualmente, no existe un formato para generar el documento. Por favor inténtelo más tarde.";
                        return View("ProblemaGenerar");
                    }
                }
            }
            catch (FileNotFoundException exFNF)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el archivo del comerciante",
                    usuario_log = "Usuario externo " + comerciante.Cedula + " y " + comerciante.Apellidos,
                    excepcion_log = exFNF.Message.ToString() + " - " + exFNF.StackTrace.ToString()
                }); 
                
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                return View("ProblemaGenerar");
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar certificado de capacitación",
                    usuario_log = "Usuario externo",
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });

                ViewBag.Mensaje = "Ha ocurrido un problema al momento de descargar el documento. Por favor inténtelo más tarde. ";
                return View("ProblemaGenerar");
            }
        }

        public ActionResult DescargarCC(int certificadoId)
        {
            try
            {
                Models.Certificados certificado = GetCertificadoById(certificadoId);
                string destinoDocumento = Server.MapPath("~/Documentos/CC/");

                // verificar si ya generó el documento para recuperarlo
                if (certificado != null)
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(destinoDocumento + certificado.ruta_archivo);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Descarga exitosa del certificado",
                        usuario_log = "Usuario externo " + certificado.Comerciantes.Cedula
                    });
                    return File(fileBytes, "application/pdf", certificado.ruta_archivo);
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el certificado en la base de datos, certificadoId=" + certificadoId,
                        usuario_log = "Usuario externo"
                    });
                    ViewBag.Mensaje = "Ha ocurrido un problema al momento de descargar el documento. Por favor inténtelo más tarde.";
                    return View("ProblemaGenerar");
                }
            }
            catch (FileNotFoundException exFNF)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el archivo del certificado",
                    usuario_log = "Usuario externo",
                    excepcion_log = exFNF.Message.ToString() + " - " + exFNF.StackTrace.ToString()
                });
                ViewBag.Mensaje = "No se encontró el archivo a descargar.";
                return View("ProblemaGenerar");
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al descargar el archivo del certificado de capacitación",
                    usuario_log = "Usuario externo",
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.Mensaje = "Ha ocurrido un error al momento de descargar el documento. Por favor inténtelo más tarde.";
                return View("ProblemaGenerar");
            }
        }

        // Ya no se utiliza ROTATIVA para generar documento - por borrar
        // no se utiliza el método anterior para generar
        //public ActionResult GenerarCertificado(Comerciantes comerciante)    // generar PDF con ROTATIVA
        //{
        //    // verificar si ya generó el documento para recuperarlo, sino se registra
        //    Models.Certificados certTemp = GetCertificadoByComerId(comerciante.Id);
        //    int resultRegistroCert;
        //    if (certTemp != null)
        //    {
        //        ViewBag.CertNumber = GetNumBaseCertificado() + certTemp.fecha_emision.Year + "-" + certTemp.num_certificado;
        //    }
        //    else
        //    {
        //        resultRegistroCert = RegistrarCertificado(comerciante);
        //        if (resultRegistroCert != 0)
        //        {
        //            ViewBag.CertNumber = GetNumBaseCertificado() + DateTime.Today.Year + "-" + resultRegistroCert;
        //        }
        //        else
        //        {
        //            return View("ProblemaGenerar");
        //        }
        //    }

        //    // generar código QR
        //    certTemp = new Models.Certificados();
        //    certTemp = GetCertificadoByComerId(comerciante.Id);
        //    ViewBag.QRCodeImage = "data:image/png;base64," + GenerarQR(certTemp.codigo_verificacion);

        //    //return new ViewAsPdf("GenerarCertificado", GetCertificadoByComerId(comerciante.Id))
        //    return new ViewAsPdf("GenerarCertificado", certTemp)
        //    {
        //        FileName = "certificado_" + comerciante.Cedula.ToString(),
        //        PageSize = Rotativa.Options.Size.A4,
        //        PageMargins = new Rotativa.Options.Margins(25, 25, 25, 25),
        //        PageOrientation = Rotativa.Options.Orientation.Portrait
        //    };
        //}

        //public int RegistrarCertificado(Comerciantes comerciante)   // registrar Certificado en la BDD
        //{
        //    // crear objeto temporal para su almacenamiento
        //    Models.Certificados certificadoTemp = new Models.Certificados
        //    {
        //        fecha_emision = DateTime.Today,
        //        comerciantes_id = comerciante.Id,
        //        codigo_verificacion = CreateHashString(comerciante.Cedula),
        //        ruta_archivo = comerciante.Apellidos.Replace(" ", "") + "_" + DateTime.Now.GetHashCode().ToString().Replace("-", "") + ".pdf",
        //        certificado_valido = true
        //    };

        //    // búsqueda número certificado actual
        //    var resultNumCert = from cert in dbContextComerciantes.Certificados
        //                        select cert.num_certificado;
        //    if (resultNumCert.Any())
        //    {
        //        var numRect = resultNumCert.Max();
        //        certificadoTemp.num_certificado = ++numRect;
        //    }
        //    else
        //    {
        //        certificadoTemp.num_certificado = 1;
        //    }

        //    // guardar el registro y revisar si fue satisfactorio
        //    dbContextComerciantes.Certificados.Add(certificadoTemp);
        //    if (dbContextComerciantes.SaveChanges() > 0)
        //    {
        //        return certificadoTemp.num_certificado;
        //    }
        //    else
        //    {
        //        return 0;
        //    }
        //}

        
        public string GenerarQR(string hashText)   // código QR para validación de certificado
        {
            // información del comerciantes que irá en el código QR
            string dataForQR = $"https://localhost:44320/Comerciantes/ValidarCertificado/{hashText}";

            // generar código QR - información
            QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
            QRCodeData qRCodeData = qRCodeGenerator.CreateQrCode(dataForQR, QRCodeGenerator.ECCLevel.Q);

            // renderizar - representación
            PngByteQRCode qrCode = new PngByteQRCode(qRCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);
            return Convert.ToBase64String(qrCodeImage);
        }

        public ActionResult RectificarDatos(Comerciantes comerciante)   // seleccionar datos a rectificar
        {
            ViewBag.Rectificado = false;
            comerciante = GetComercianteById(comerciante.Id);
            ViewData["institucionesDMQ"] = GetItemsInstitucionesDMQ(GetInstituciones(), comerciante.instituciones_id);

            ComerciantesRectificacionesView comerRectView = new ComerciantesRectificacionesView();
            comerRectView = LlenarComerRectView(comerRectView, comerciante);
            return View(comerRectView);
        }

        public ComerciantesRectificacionesView LlenarComerRectView(ComerciantesRectificacionesView comerRectView, Comerciantes comerciante)
        {
            comerRectView.comer_id = comerciante.Id;
            comerRectView.Nombres = comerciante.Nombres;
            comerRectView.Apellidos = comerciante.Apellidos;
            comerRectView.Cedula = comerciante.Cedula;
            comerRectView.Capacitacion = comerciante.Capacitacion;
            comerRectView.insti_id = comerciante.instituciones_id;
            comerRectView.nombre_institucion = comerciante.Institucion2.nombre_institucion;
            comerRectView.rectificar_inst_origen = comerciante.instituciones_id;
            comerRectView.acronimo_institucion = comerciante.Institucion2.acronimo_institucion;

            return comerRectView;
        }

        public ActionResult GenerarRectificacion()
        {
            return View("NuevaBusqueda");
        }

        [HttpPost]
        public ActionResult GenerarRectificacion(ComerciantesRectificacionesView comerRectView)
        {
            try
            {
                Rectificaciones rectTemp = GetRectificacionById(comerRectView.recti_id);
                Comerciantes comerTemp = GetComercianteById(comerRectView.comer_id);
                string pathDestinoDocumento = Server.MapPath("~/Documentos/Rect/");

                // verificar si ya generó el documento para recuperarlo, caso contrario se registra
                if (rectTemp != null)
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(pathDestinoDocumento + rectTemp.ruta_archivo);
                    return File(fileBytes, "application/pdf", rectTemp.ruta_archivo);
                }
                else
                {
                    // validación de rectificacion campos
                    if (comerRectView.rectificar_nom_ape || comerRectView.rectificar_cedula || 
                        comerRectView.rectificar_inst_destino != 0)
                    {
                        // verificar que exista una plantilla cargada para generar el CC
                        PlantillasCC plantillaRect = GetArchivoPlantillaRectActiva();
                        if (plantillaRect != null)
                        {
                            // DOCUMENTO: generar pdf y registrar objeto
                            // plantilla certificado path
                            string pathArchivoPlantilla = "~/PlantillasCC/";
                            string nombreArchivoPlantilla = plantillaRect.archivo_plantilla;

                            // si no se ha generado, crear el documento, registrar y enviar para descarga
                            rectTemp = new Rectificaciones
                            {
                                rectificar_nom_ape = comerRectView.rectificar_nom_ape,
                                rectificar_cedula = comerRectView.rectificar_cedula,
                                rectificar_inst_origen = comerRectView.rectificar_inst_origen,
                                rectificar_inst_destino = comerRectView.rectificar_inst_destino,
                                comerciantes_id = comerTemp.Id,
                                fecha_rectificar = DateTime.Now,
                                num_solicitud = GetNumeroRectificacion(),
                                plantillascc_id = plantillaRect.Id,
                                ruta_archivo = "rect_" + comerTemp.Cedula.Trim().Replace(" ", "") + "_" + DateTime.Now.GetHashCode().ToString().Replace("-", "") + ".pdf",
                                solicitud_atendida = false
                            };

                            if (!String.IsNullOrEmpty(nombreArchivoPlantilla))
                            {
                                // abrir plantilla
                                var pdfReader = new PdfReader(Request.MapPath(pathArchivoPlantilla + nombreArchivoPlantilla));

                                // buffer para almacenar
                                MemoryStream ms = new MemoryStream();
                                PdfWriter pw = new PdfWriter(ms);
                                PdfDocument pdf = new PdfDocument(pdfReader, pw);
                                Document doc = new Document(pdf, PageSize.A4);

                                // mapeo de datos - campos obligatorios
                                PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                                IDictionary<String, PdfFormField> fields = form.GetFormFields();

                                // texto sobre campos a rectificar
                                List<String> listaDatosRectificar = new List<string>();
                                if (rectTemp.rectificar_nom_ape)
                                {
                                    listaDatosRectificar.Add("Rectificar nombres y/o apellidos");
                                }
                                if (rectTemp.rectificar_cedula)
                                {
                                    listaDatosRectificar.Add("Rectificar el Número de identificación (cédula)");
                                }
                                if (rectTemp.rectificar_inst_destino != 0)
                                {
                                    listaDatosRectificar.Add("Realizar el Cambio de Institución de la \'" +
                                        GetInstitucionById(rectTemp.rectificar_inst_origen).nombre_institucion.ToUpper() + "\' a la \'" +
                                        GetInstitucionById(rectTemp.rectificar_inst_destino).nombre_institucion.ToUpper() + "\'");
                                }
                                string textoRectificar = String.Join("; ", listaDatosRectificar) + ".";

                                fields.TryGetValue("numero_rect", out PdfFormField toSet);
                                toSet.SetValue(GetNumBaseRectificacion() + DateTime.Today.Year + "-" + rectTemp.num_solicitud);
                                fields.TryGetValue("fecha_rect", out toSet);
                                toSet.SetValue(rectTemp.fecha_rectificar.ToString("dd/MM/yyyy"));
                                fields.TryGetValue("coordinadorACDC", out toSet);
                                toSet.SetValue(GetCoordinadorACDC());
                                fields.TryGetValue("datos_rectificar", out toSet);
                                toSet.SetValue(textoRectificar);

                                form.FlattenFields();
                                doc.Close();

                                // guardar bytes
                                byte[] byteStream = ms.ToArray();
                                ms = new MemoryStream();
                                ms.Write(byteStream, 0, byteStream.Length);
                                ms.Position = 0;

                                // guardar en servidor
                                FileStream fileStream = new FileStream(pathDestinoDocumento + rectTemp.ruta_archivo, FileMode.Create, FileAccess.ReadWrite);
                                ms.WriteTo(fileStream);
                                fileStream.Close();

                                // registrar en Tabla Rectificaciones
                                dbContextComerciantes.Rectificaciones.Add(rectTemp);
                                if (dbContextComerciantes.SaveChanges() > 0)
                                {
                                    log.Transaction(new Log_Registros()
                                    {
                                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera Rectificiación y se registra en la base de datos",
                                        usuario_log = "Usuario externo " + comerTemp.Cedula + " y " + comerTemp.Apellidos
                                    });

                                    return View(GetRectificacionById(rectTemp.Id));
                                }
                                else
                                {
                                    BorrarArchivo(pathDestinoDocumento + rectTemp.ruta_archivo);
                                    log.Warning(new Log_Registros()
                                    {
                                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se guardó en base de datos el objeto rectificaciones",
                                        usuario_log = "Usuario externo " + comerTemp.Cedula + " y " + comerTemp.Apellidos
                                    });
                                    ViewBag.Mensaje = "Ha ocurrido un problema al momento de generar el documento. Por favor inténtelo más tarde.";
                                    return View("ProblemaGenerar");
                                }
                            }
                            else
                            {
                                log.Warning(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Existe un problema con la plantilla Rect",
                                    usuario_log = "Usuario externo " + comerTemp.Cedula + " y " + comerTemp.Apellidos
                                });
                                ViewBag.Mensaje = "Ha ocurrido un problema al momento de generar el documento. Por favor inténtelo más tarde.";
                                return View("ProblemaGenerar");
                            }
                        }
                        else
                        {
                            // no se carga aún la plantilla  Rect
                            log.Warning(new Log_Registros()
                            {
                                mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra plantilla Rect para generar",
                                usuario_log = "Usuario externo " + comerTemp.Cedula + " y " + comerTemp.Apellidos
                            });
                            ViewBag.Mensaje = "Actualmente, no existe un formato para generar el documento. Por favor inténtelo más tarde.";
                            return View("ProblemaGenerar");
                        }
                    }
                    else
                    {
                        // no se marca casillas para indicar campos a rectificar
                        comerRectView = LlenarComerRectView(comerRectView, comerTemp);
                        ViewBag.Mensaje = "Debe indicar los campos que desea rectificar.";
                        ViewData["institucionesDMQ"] = GetItemsInstitucionesDMQ(GetInstituciones(), comerRectView.insti_id);
                        log.Info(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Intenta generar rectificación sin marcar campos",
                            usuario_log = "Usuario externo " + comerTemp.Cedula + " y " + comerTemp.Apellidos
                        });
                        return View("RectificarDatos", comerRectView);
                    }
                }
            }
            catch (FileNotFoundException exFNF)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el archivo de rectificacion",
                    usuario_log = "Usuario externo",
                    excepcion_log = exFNF.Message.ToString() + " - " + exFNF.StackTrace.ToString()
                });

                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                return View("ProblemaGenerar");
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar la solicitud de rectificación",
                    usuario_log = "Usuario externo",
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });

                ViewBag.Mensaje = "Ha ocurrido un problema al momento de descargar el documento. Por favor inténtelo más tarde. ";
                return View("ProblemaGenerar");
            }
        }

        public ActionResult DescargarRect(int rectificacionId)
        {
            try
            {
                Rectificaciones rectificacion = GetRectificacionById(rectificacionId);
                string destinoDocumento = Server.MapPath("~/Documentos/Rect/");

                // verificar si ya generó el documento para recuperarlo, caso contrario se registra
                if (rectificacion != null)
                {
                    // si se ha generado, abrir documento y enviar para descarga
                    byte[] fileBytes = System.IO.File.ReadAllBytes(destinoDocumento + rectificacion.ruta_archivo);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Descarga exitosa de la rectificación",
                        usuario_log = "Usuario externo " + rectificacion.Comerciantes.Cedula
                    });
                    return File(fileBytes, "application/pdf", rectificacion.ruta_archivo);
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el certificado en la base de datos, rectificacionID=" + rectificacionId,
                        usuario_log = "Usuario externo"
                    });
                    ViewBag.Mensaje = "Ha ocurrido un problema al momento de descargar el documento. Por favor inténtelo más tarde.";
                    return View("ProblemaGenerar");
                }
            }
            catch (FileNotFoundException exFNF)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el archivo de la rectificación",
                    usuario_log = "Usuario externo",
                    excepcion_log = exFNF.Message.ToString() + " - " + exFNF.StackTrace.ToString()
                });
                ViewBag.Mensaje = "No se encontró el archivo a descargar.";
                return View("ProblemaGenerar");
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al descargar el archivo de rectificación",
                    usuario_log = "Usuario externo",
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.Mensaje = "Ha ocurrido un error al momento de descargar el documento. Por favor inténtelo más tarde.";
                return View("ProblemaGenerar");
            }
        }

        public ActionResult ValidarCertificado(string id)  // validar certificado generado
        {
            try
            {
                Comerciantes comerciante = GetComercianteByCodigoVerificacion(id);
                ViewBag.CertNumber = GetNumeroCertificado();
                if (comerciante != null)
                {
                    log.Info(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Validación de certificado exitosa, comerciante: " + comerciante.Cedula,
                        usuario_log = "Usuario externo"
                    });
                    return View(GetCertificadoByComerId(comerciante.Id));
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra comerciante con código validación: " + id,
                        usuario_log = "Usuario externo"
                    });
                    ViewBag.Mensaje = "Certificado sin validez.";
                    return View("NoValidacion");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al validar con código: " + id,
                    usuario_log = "Usuario externo",
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.Mensaje = "Hubo un problema con la validación. Por favor, inténtelo más tarde.";
                return View("NoValidacion");
            }
        }


        // métodos para obtener información de la base de datos ------------------------------------

        public Comerciantes GetComercianteById(int comerId)      // recuperar Comerciante por su Id
        {
            var resultado = from com in dbContextComerciantes.Comerciantes
                            where com.Id == comerId
                            select com;
            return resultado.Any() ? resultado.First() : null;
        }

        public Comerciantes GetComercianteByCedula(string cedulaString)    // buscar Comerciante por cedulaString
        {
            var resultado = from c in dbContextComerciantes.Comerciantes
                            where c.Cedula == cedulaString
                            select c;
            return resultado.Any() ? resultado.First() : null;
        }

        public Comerciantes GetComercianteByCedulaApellidos(string cedulaString, string apellidosString)    // buscar Comerciante
        {
            var resultado = from c in dbContextComerciantes.Comerciantes
                            where c.Cedula == cedulaString && c.Apellidos == apellidosString
                            select c;
            return resultado.Any() ? resultado.First() : null;
        }

        public Comerciantes GetComercianteByCodigoVerificacion(string codigoVerificacion)    // buscar Comerciante por cedulaString
        {
            var resultado = from com in dbContextComerciantes.Comerciantes
                            join cert in dbContextComerciantes.Certificados on com.Id equals cert.comerciantes_id
                            where cert.codigo_verificacion == codigoVerificacion
                            select com;
            return resultado.Any() ? resultado.First() : null;
        }

        public Models.Certificados GetCertificadoById(int certificadoId) // recuperar Certificado por id comerciante
        {
            var resultado = from cert in dbContextComerciantes.Certificados
                            where cert.Id == certificadoId && cert.certificado_valido == true
                            select cert;
            return resultado.Any() ? resultado.First() : null;
        }

        public Models.Certificados GetCertificadoByComerId(int comerId) // recuperar Certificado por id comerciante
        {
            var resultado = from cert in dbContextComerciantes.Certificados
                            where cert.comerciantes_id == comerId && cert.certificado_valido == true
                            select cert;
            return resultado.Any() ? resultado.First() : null;
        }

        public Rectificaciones GetRectificacionByComerId(int comerId)   // recuperar Rectificacion no atendida por id comerciante
        {
            var resultado = from rect in dbContextComerciantes.Rectificaciones
                            where rect.comerciantes_id == comerId && rect.solicitud_atendida == false
                            select rect;
            return resultado.Any() ? resultado.First() : null;
        }

        public Rectificaciones GetRectificacionById(int id)   // recuperar Rectificacion no atendida por id comerciante
        {
            var resultado = from rect in dbContextComerciantes.Rectificaciones
                            where rect.Id == id && rect.solicitud_atendida == false
                            select rect;
            return resultado.Any() ? resultado.First() : null;
        }

        public Institucion GetInstitucionById(int id)    // recuperar Institucion por su Id
        {
            var resultado = from inst in dbContextComerciantes.Institucion
                            where inst.Id == id
                            select inst;
            return resultado.Any() ? resultado.First() : null;
        }

        public List<Institucion> GetInstituciones()    // recuperar listados de instituciones
        {
            var resultado = from inst in dbContextComerciantes.Institucion
                            select inst;
            return resultado.Any() ? resultado.ToList() : null;
        }

        public List<SelectListItem> GetItemsInstitucionesDMQ(List<Institucion> institucionesDMQ, int institucion_id)   // recupera InstitucionesDMQ para drop-down
        {
            var listItemsInstituciones = new List<SelectListItem>();
            foreach (var inst in institucionesDMQ)
            {
                bool selectedItem = institucion_id == inst.Id;
                listItemsInstituciones.Add(new SelectListItem { Selected = selectedItem, Text = inst.nombre_institucion, Value = inst.Id.ToString() });
            }
            return listItemsInstituciones;
        }

        public string CreateHashString(string text)  // convierte String a SHA256 - String
        {
            if (!String.IsNullOrEmpty(text))
            {
                // inicializar el objeto SHA
                using (var sha = new SHA256Managed())
                {

                    // convierte string a un array de bytes y calcular el hash
                    byte[] textBytes = Encoding.UTF8.GetBytes(text + GetSalt());
                    byte[] hashBytes = sha.ComputeHash(textBytes);

                    // convierte el array de bytes en String y remueve el "-" de BitConverter
                    string hash = BitConverter.ToString(hashBytes).Replace("-", String.Empty);
                    return hash;
                }
            }
            else
            {
                return String.Empty;
            }
        }

        public byte[] GenerarQRByte(string hashText)   // generar código QR con información del Comerciante
        {
            string dataForQR = hashText;

            // generar código QR - información
            QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
            QRCodeData qRCodeData = qRCodeGenerator.CreateQrCode(dataForQR, QRCodeGenerator.ECCLevel.Q);

            // renderizar - representación
            PngByteQRCode qrCode = new PngByteQRCode(qRCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);
            return qrCodeImage;
        }

        public int GetNumeroCertificado()
        {
            var resultNumCert = from cert in dbContextComerciantes.Certificados
                                select cert.num_certificado;
            if (resultNumCert.Any())
            {
                var numCert = resultNumCert.Max();
                return ++numCert;
            }
            else
            {
                return 1;
            }
        }

        public int GetNumeroRectificacion()
        {
            var resultNumRect = from rect in dbContextComerciantes.Rectificaciones
                                select rect.num_solicitud;
            if (resultNumRect.Any())
            {
                var numRect = resultNumRect.Max();
                return ++numRect;
            }
            else
            {
                return 1;
            }
        }

        public string GetCoordinadorACDC()
        {
            var resultado = (from p in dbContextComerciantes.ParametrosCC
                             where p.nombre_parametro == "CoordinadorACDC"
                             select p.valor_parametro).First();
            return resultado;
        }

        public string GetDirectorDCA()
        {
            var resultado = (from p in dbContextComerciantes.ParametrosCC
                             where p.nombre_parametro == "DirectorDCA"
                             select p.valor_parametro).First();
            return resultado;
        }

        public string GetNumBaseCertificado()
        {
            var resultado = (from p in dbContextComerciantes.ParametrosCC
                             where p.nombre_parametro == "NumBaseCertificado"
                             select p.valor_parametro).First();
            return resultado;
        }

        public string GetNumBaseRectificacion()
        {
            var resultado = (from p in dbContextComerciantes.ParametrosCC
                             where p.nombre_parametro == "NumBaseRectificacion"
                             select p.valor_parametro).First();
            return resultado;
        }

        public string GetSalt()
        {
            var resultado = (from p in dbContextComerciantes.ParametrosCC
                             where p.nombre_parametro == "SaltHash"
                             select p.valor_parametro).First();
            return resultado;
        }

        public void BorrarArchivo(string path)
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        public PlantillasCC GetArchivoPlantillaCCActiva()
        {
            var resultado = from pcc in DbContextComerciantes.PlantillasCC
                            where pcc.plantilla_activa == true && pcc.documento_plantilla == "certificado"
                            select pcc;
            return resultado.Any() ? resultado.First() : null;
        }

        public PlantillasCC GetArchivoPlantillaRectActiva()
        {
            var resultado = from pcc in DbContextComerciantes.PlantillasCC
                            where pcc.plantilla_activa == true && pcc.documento_plantilla == "rectificacion"
                            select pcc;
            return resultado.Any() ? resultado.First() : null;
        }
    } //Fin Controlador
} // Fin de NameSpace