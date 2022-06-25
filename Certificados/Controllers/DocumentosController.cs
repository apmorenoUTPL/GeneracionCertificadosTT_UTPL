using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Certificados.Models;
using System.IO;
using System.IO.Compression;
using Certificados.Models.ViewModel;
using Certificados.LogHistorial;

namespace Certificados.Controllers
{
    [Authorize]
    public class DocumentosController : Controller
    {
        private readonly ComerciantesEntities dbContextPlantilla = new ComerciantesEntities();
        public Log log = new Log();

        public ComerciantesEntities DbContextPlantilla => dbContextPlantilla;

        // GET: Documentos
        public ActionResult Index()
        {
            return View("IndexDocumentos");
        }

        public ActionResult IndexDocumentos()
        {
            return View();
        }

        public JsonResult ListarDocumentos()
        {
            List<DocumentoViewModel> listaResultado = new List<DocumentoViewModel>();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                listaResultado = (from doc in db.Documentos
                                  join dato in db.Datos on doc.dato_id equals dato.Id
                                  select new DocumentoViewModel
                                  {
                                      Nombres_Apellidos = dato.nombres_apellidos,
                                      Curso_Taller = dato.curso_taller,
                                      Fecha_Curso_Taller = dato.fecha,
                                      Fecha = doc.fecha_generado.ToString(),
                                      Archivo = doc.ruta_archivo,
                                      Codigo_Verificacion = doc.codigo_verificacion
                                  }
                                  ).ToList();
            }
            return Json(new { data = listaResultado }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult VerDocumento(string rutaArchivo)
        {
            try
            {
                string pathDescarga = Server.MapPath("~/Documentos/OtrosDocumentos/");
                if (System.IO.File.Exists(pathDescarga + rutaArchivo))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + rutaArchivo);
                    log.Error(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Descarga de archivo Documentos: " + pathDescarga + rutaArchivo,
                        usuario_log = User.Identity.Name
                    });

                    return File(fileBytes, "application/pdf", rutaArchivo);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            catch (FileNotFoundException exFNF)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se encuentra el archivo Documentos: " + rutaArchivo,
                    usuario_log = User.Identity.Name,
                    excepcion_log = exFNF.Message.ToString() + " - " + exFNF.StackTrace.ToString()
                });
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                //ViewBag.MensajeError = "Error: " + fileNotFoundException.Message.ToString();
                return View("ReporteError");
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al cargar intentar descargar archivo Documentos: " + rutaArchivo,
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.Mensaje = "Se ha presentado un problema al intentar descargar el documento.";
                //ViewBag.MensajeError = "Error: " + ex.Message.ToString();
                return View("ReporteError");
            }
        }

        public ActionResult DetallarDocumento(int idDoc)
        {
            try
            {
                Documentos docDetalles = GetDocumentoById(idDoc);
                if (docDetalles != null)
                {
                    log.Info(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Se recupera detalles de Documento con id=" + idDoc,
                        usuario_log = User.Identity.Name
                    });
                    return View(docDetalles);
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se ha encontrado Documento con id=" + idDoc,
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se encuentran detalles de este documento.";
                    ViewBag.MensajeError = "Por favor, intente más tarde";
                    return View("ReporteError");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al detallar documento",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                return View("ReporteError");
            }
        }

        // no se utiliza
        public JsonResult Descargar(string[] docsIdArray)   // prueba ResultJson a AJAX para descarga
        {
            string listaString = String.Empty;
            foreach (var docId in docsIdArray)
            {
                listaString = listaString + docId + ",";
            }
            return Json(data: new { lista = listaString });
        }

        // no se utiliza
        public ActionResult DescargarZipDocumentos(string[] docsIdArray)    // descarga ZIP de documentos generados
        {
            try
            {
                if (docsIdArray != null || docsIdArray.Length > 0)
                {
                    // obtener lista de documentos generados
                    List<Documentos> listaDocumentos = new List<Documentos>();
                    foreach (var item in docsIdArray)
                    {
                        if (Int32.TryParse(item, out int datoId))
                        {
                            listaDocumentos.Add(GetDocumentoById(datoId));
                        }
                    }

                    // creación del archivo ZIP
                    string pathDescarga = Server.MapPath("~/Documentos/OtrosDocumentos/");
                    using (var ms = new MemoryStream())
                    {
                        using (var archivo = new ZipArchive(ms, ZipArchiveMode.Create, true))
                        {
                            foreach (Documentos doc in listaDocumentos)
                            {
                                var entry = archivo.CreateEntry(doc.ruta_archivo);
                                using (var entryStream = entry.Open())
                                using (var fileStream = System.IO.File.OpenRead(pathDescarga + doc.ruta_archivo))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                        }
                        return File(ms.ToArray(), "application/zip", "documentos.zip");
                    };
                }
                else
                {
                    ViewBag.Mensaje = "Debe seleccionar al menos un documento para iniciar la descarga.";
                    return RedirectToAction("ListarDocumentos");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No se ha podido atender su requerimiento debido a un error del sistema.";
                ViewBag.MensajeError = "Por favor, intente más tarde." + "Error: " + ex.Message.ToString();
                return View("ReporteError");
            }
        }

        // métodos de soporte --------------------------------------

        public List<Documentos> GetDocumentos()
        {
            var result = from d in dbContextPlantilla.Documentos
                         select d;
            return result.Any() ? result.ToList() : null;
        }

        public Documentos GetDocumentoById(int idDoc)
        {
            var result = from d in dbContextPlantilla.Documentos
                         where d.Id == idDoc
                         select d;
            return result.Any() ? result.First() : null;
        }
    }
}