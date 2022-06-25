using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Certificados.Models;
using Certificados.Models.ViewModel;
using Certificados.ReportesFuente;
using Certificados.LogHistorial;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace Certificados.Controllers
{
    [Authorize]
    public class ConsultaController : Controller
    {
        // objeto LOG
        public Log log = new Log();

        // GET: Consulta
        // no se utiliza vista para rol CONSULTA
        // se emplea el Index de TECNICO
        public ActionResult Index()
        {
            return View();
        }

        // index de REPORTES GENERALES
        public ActionResult ReportesIndex()
        {
            return View();
        }

        // no se utiliza este método en la vista
        //public JsonResult Listar()
        //{
        //    List<ComercianteViewModel> lst = new List<ComercianteViewModel>();
        //    using (ComerciantesEntities db = new ComerciantesEntities())
        //    {
        //        lst = (from p in db.Comerciantes
        //               join a in db.Institucion
        //               on p.instituciones_id equals a.Id
        //               select new ComercianteViewModel
        //               {
        //                   Id = p.Id,
        //                   Nombres = p.Nombres,
        //                   Apellidos = p.Apellidos,
        //                   Cedula = p.Cedula,
        //                   Capacitacion = p.Capacitacion,
        //                   Institucion = a.nombre_institucion
        //               }
        //               ).ToList();
        //    }
        //    return Json(new { data = lst }, JsonRequestBehavior.AllowGet);
        //}        
        
        // no se utiliza este reporte
        public ActionResult VerReporteCapacitados(int id)
        {
            try
            {
                var reporte = new ReportClass();

                if (id == 0)
                {
                    reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCapaInst.rpt");
                    reporte.Load();
                }
                else
                {
                    reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCapaByInst.rpt");
                    reporte.Load();
                    reporte.SetParameterValue("institucionId", id);
                }

                //reporte.SetParameterValue("fechaInicial", fechaInicial);
                //reporte.SetParameterValue("fechaFinal", fechaFinal);

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                // export PDF
                Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                reporte.Dispose();
                reporte.Close();
                return new FileStreamResult(stream, "application/pdf");

                // export Excel
                //Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                //stream.Seek(0, SeekOrigin.Begin);
                //return File(stream, "application/vnd.ms-excel", "reporte");
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        // no se utiliza este reporte
        public ActionResult VerReporteTotalCapaInst()
        {
            try
            {
                var reporte = new ReportClass();
                reporte.FileName = Server.MapPath("/ReportesFuente/ReporteTotalCapaInst.rpt");
                reporte.Refresh();

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                // export PDF
                Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                reporte.Dispose();
                reporte.Close();
                return new FileStreamResult(stream, "application/pdf");

                // export Excel
                //Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                //stream.Seek(0, SeekOrigin.Begin);
                //return File(stream, "application/vnd.ms-excel", "reporte");
            }
            catch (Exception ex)
            {

                throw;
            }
        }


        // index de REPORTES CAPACITACION ------------------------------------------------------
        // comerciantes capacitador por institución y por año de capacitación
        public ActionResult ReportesCapacitacion()
        {
            return View();
        }

        public ActionResult VerReporteAnioCapacitados(int id, string capacitacion, string tipo)
        {
            try
            {
                var reporte = new ReportClass();

                if (id == 0)
                {
                    reporte.FileName = Server.MapPath("/ReportesFuente/ReporteAnioCapa.rpt");
                    reporte.Load();
                }
                else
                {
                    reporte.FileName = Server.MapPath("/ReportesFuente/ReporteAnioCapabyInst.rpt");
                    reporte.Load();
                    reporte.SetParameterValue("institucionId", id);
                }
                reporte.SetParameterValue("AnioCapacitacion", capacitacion);

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }

        public ActionResult VerReporteAnioTotalCapacitados(string capacitacion, string tipo)
        {
            try
            {
                var reporte = new ReportClass();

                reporte.FileName = Server.MapPath("/ReportesFuente/ReporteAnioTotalCapa.rpt");
                reporte.Load();
                reporte.SetParameterValue("AnioCapacitacion", capacitacion);

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }


        // index de REPORTES CERTIFICADOS GENERADOS ------------------------------------------------------
        public ActionResult ReportesCertificados()
        {
            return View();
        }

        public ActionResult VerReporteCertGen(int id, string anioBusqueda, string tipo)
        {
            try
            {
                var reporte = new ReportClass();
                anioBusqueda = anioBusqueda.Trim();

                if (id == 0)
                {
                    if (!String.IsNullOrWhiteSpace(anioBusqueda) && anioBusqueda != "0")
                    {
                        if (Int32.TryParse(anioBusqueda, out int anio) && (anio >= 2000 && anio <= DateTime.Today.Year))
                        {
                            reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCertGenAnio.rpt");
                            reporte.Load();

                            var fechaInicio = new DateTime(anio, 1, 1);
                            var fechaFin = new DateTime(anio + 1, 1, 1);

                            reporte.SetParameterValue("Inicio", fechaInicio);
                            reporte.SetParameterValue("Final", fechaFin);
                        }
                    }
                    else
                    {
                        reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCertGen.rpt");
                        reporte.Load();
                    }                    
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(anioBusqueda) && anioBusqueda != "0")
                    {
                        if (Int32.TryParse(anioBusqueda, out int anio) && (anio >= 2000 && anio <= DateTime.Today.Year))
                        {
                            reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCertGenByInstAnio.rpt");
                            reporte.Load();

                            var fechaInicio = new DateTime(anio, 1, 1);
                            var fechaFin = new DateTime(anio, 12, 31);

                            reporte.SetParameterValue("Inicio", fechaInicio);
                            reporte.SetParameterValue("Final", fechaFin);
                        }
                    }
                    else
                    {
                        reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCertGenByInst.rpt");
                        reporte.Load();
                        reporte.SetParameterValue("institucionId", id);
                    }               
                }

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }

        public ActionResult VerReporteTotalCertInst( string anioBusqueda, string tipo)
        {
            try
            {
                var reporte = new ReportClass();
                anioBusqueda = anioBusqueda.Trim();

                if (!String.IsNullOrWhiteSpace(anioBusqueda) && anioBusqueda != "0")
                {
                    if (Int32.TryParse(anioBusqueda, out int anio) && (anio >= 2000 && anio <= DateTime.Today.Year))
                    {
                        reporte.FileName = Server.MapPath("/ReportesFuente/ReporteCertInstAnioTotal.rpt");
                        reporte.Load();

                        var fechaInicio = new DateTime(anio, 1, 1);
                        var fechaFin = new DateTime(anio + 1, 1, 1);

                        reporte.SetParameterValue("Inicio", fechaInicio);
                        reporte.SetParameterValue("Final", fechaFin);
                    }
                    else
                    {
                        ViewBag.Mensaje = "No se han encontrado resultados con " + anioBusqueda;
                        return View("SinResultados");
                    }
                }
                else
                {
                    ViewBag.Mensaje = "Ingrese un valor comprendido entre el 2000 y " + DateTime.Now.Year.ToString();
                    return View("SinResultados");
                }

                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }


        // index de REPORTES RECTIFICACIONES GENERADAS ------------------------------------------------------
        public ActionResult ReportesRectificaciones()
        {
            return View();
        }

        public ActionResult VerReporteRectGen(string anioBusqueda, string tipo)
        {
            try
            {
                var reporte = new ReportClass();
                anioBusqueda = anioBusqueda.Trim();

                if (!String.IsNullOrWhiteSpace(anioBusqueda) && anioBusqueda != "0")
                {
                    if (Int32.TryParse(anioBusqueda, out int anio) && (anio >= 2000 && anio <= DateTime.Today.Year))
                    {
                        reporte.FileName = Server.MapPath("/ReportesFuente/ReporteRectGenAnio.rpt");
                        reporte.Load();

                        var fechaInicio = new DateTime(anio, 1, 1);
                        var fechaFin = new DateTime(anio + 1, 1, 1);

                        reporte.SetParameterValue("Inicio", fechaInicio);
                        reporte.SetParameterValue("Final", fechaFin);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }                
                
                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }

        public ActionResult VerReporteRectAtendidasAnio(string anioBusqueda, string tipo)
        {
            try
            {
                var reporte = new ReportClass();
                anioBusqueda = anioBusqueda.Trim();

                if (!String.IsNullOrWhiteSpace(anioBusqueda) && anioBusqueda != "0")
                {
                    if (Int32.TryParse(anioBusqueda, out int anio) && (anio >= 2000 && anio <= DateTime.Today.Year))
                    {
                        reporte.FileName = Server.MapPath("/ReportesFuente/ReporteRectAtendidasAnio.rpt");
                        reporte.Load();

                        var fechaInicio = new DateTime(anio, 1, 1);
                        var fechaFin = new DateTime(anio + 1, 1, 1);

                        reporte.SetParameterValue("Inicio", fechaInicio);
                        reporte.SetParameterValue("Final", fechaFin);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
                
                //Conexión para el reporte
                var coninfo = ReportesConexion.GetConnection();
                TableLogOnInfo logOnInfo = new TableLogOnInfo();
                Tables tables;
                tables = reporte.Database.Tables;

                Response.Buffer = false;
                Response.ClearContent();
                Response.ClearHeaders();

                if (tipo == "PDF")
                {
                    // export PDF
                    Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                    reporte.Dispose();
                    reporte.Close();
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return new FileStreamResult(stream, "application/pdf");
                }
                else if (tipo == "EXCEL")
                {
                    // export Excel
                    Stream stream = reporte.ExportToStream(ExportFormatType.Excel);
                    stream.Seek(0, SeekOrigin.Begin);
                    log.Transaction(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Genera reporte PDF",
                        usuario_log = User.Identity.Name
                    });
                    return File(stream, "application/vnd.ms-excel", "reporte");
                }
                else
                {
                    log.Warning(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se generó el reporte ni PDF ni EXCEL",
                        usuario_log = User.Identity.Name
                    });
                    ViewBag.Mensaje = "No se pudo generar el reporte.";
                    return View("SinResultado");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al generar el reporte",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                throw;
            }
        }

    }
}