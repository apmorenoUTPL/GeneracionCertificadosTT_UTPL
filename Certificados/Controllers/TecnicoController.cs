using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Certificados.Models;
using Certificados.Models.ViewModel;

namespace Certificados.Controllers
{
    [Authorize]
    public class TecnicoController : Controller
    {
        // GET
        // registros de comerciantes - rol: Tecnico y Consulta
        public ActionResult Index()
        {
            List<InstitucionViewModel> inst = null;
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                inst = (from d in db.Institucion
                        select new InstitucionViewModel
                        {
                            Id = d.Id,
                            Nombre = d.nombre_institucion
                        }).ToList();
            }

            List<SelectListItem> items = inst.ConvertAll(d =>
            {
                return new SelectListItem()
                {
                    Text = d.Nombre.ToString(),
                    Value = d.Id.ToString(),
                };
                
            });

            ViewBag.items = items;
            return View();
        }

        // rectificaciones por atender - rol: Tecnico
        public ActionResult SolicitudesRectificacion()
        {
            List<InstitucionViewModel> inst = null;
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                inst = (from d in db.Institucion
                        select new InstitucionViewModel
                        {
                            Id = d.Id,
                            Nombre = d.nombre_institucion
                        }).ToList();
            }
            List<SelectListItem> items = inst.ConvertAll(d =>
            {

                return new SelectListItem()
                {
                    Text = d.Nombre.ToString(),
                    Value = d.Id.ToString(),
                };

            });
            ViewBag.items = items;
            return View();
        }


        public JsonResult Listar()
        {
            List<ComercianteViewModel> lst = new List<ComercianteViewModel>();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                lst = (from p in db.Comerciantes
                       join a in db.Institucion on p.instituciones_id equals a.Id
                       select new ComercianteViewModel
                       {
                           Id = p.Id,
                           Nombres = p.Nombres,
                           Apellidos = p.Apellidos,
                           Cedula = p.Cedula,
                           Capacitacion = p.Capacitacion,
                           Institucion = a.nombre_institucion
                       }
                       ).ToList();
            }
            return Json(new { data = lst }, JsonRequestBehavior.AllowGet);
        }


        // lista registros de comerciantes
        public JsonResult ListarComerciantes()
        {
            List<ComercianteEstadoCCViewModel> listaResultado = new List<ComercianteEstadoCCViewModel>();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                listaResultado = (from comer in db.Comerciantes
                                  select new ComercianteEstadoCCViewModel
                                  {
                                       Id = comer.Id,
                                       Nombres = comer.Nombres,
                                       Apellidos = comer.Apellidos,
                                       Cedula = comer.Cedula,
                                       Capacitacion = comer.Capacitacion,
                                       Institucion = comer.Institucion2.nombre_institucion,
                                       CertificadoGenerado = String.Empty
                                  }).ToList();

                foreach (ComercianteEstadoCCViewModel cc in listaResultado)
                {
                    Models.Certificados certificadoTemp = GetCertificadoByComercianteId(cc.Id);
                    cc.CertificadoGenerado = certificadoTemp != null ? certificadoTemp.ruta_archivo : "NO";
                }
            }
            return Json(new { data = listaResultado }, JsonRequestBehavior.AllowGet);
        }
                

        // lista rectificaciones pendientes de atención
        public JsonResult ListarSolicitudesRectificacion()
        {
            List<ComercianteEstadoRectViewModel> listaResultado = new List<ComercianteEstadoRectViewModel>();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                listaResultado = (from comer in db.Comerciantes
                       join rect in db.Rectificaciones on comer.Id equals rect.comerciantes_id
                       where rect.solicitud_atendida == false
                       select new ComercianteEstadoRectViewModel
                           {
                               Id = comer.Id,
                               Nombres = comer.Nombres,
                               Apellidos = comer.Apellidos,
                               Cedula = comer.Cedula,
                               Capacitacion = comer.Capacitacion.ToString(),
                               Institucion = comer.Institucion2.nombre_institucion,
                               RectificacionGenerada = String.Empty
                           }).ToList();
            }

            foreach (ComercianteEstadoRectViewModel cc in listaResultado)
            {
                Rectificaciones rectificacionTemp = GetRectificacionByComercianteId(cc.Id);
                cc.RectificacionGenerada = rectificacionTemp != null ? rectificacionTemp.ruta_archivo : "NO";
            }



            return Json(new { data = listaResultado }, JsonRequestBehavior.AllowGet);
        }


        public JsonResult Obtener(int ID)
        {
            ComercianteViewModel oComerciante = new ComercianteViewModel();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                oComerciante = (from p in db.Comerciantes
                       join a in db.Institucion on p.Id equals ID
                       join rect in db.Rectificaciones on p.Id equals rect.comerciantes_id

                       select new ComercianteViewModel
                       {
                           Id = p.Id,
                           Nombres = p.Nombres,
                           Apellidos = p.Apellidos,
                           Cedula = p.Cedula,
                           Capacitacion = p.Capacitacion,
                           Institucion = a.nombre_institucion
                       }).ToList().FirstOrDefault();
            }
            return Json(oComerciante, JsonRequestBehavior.AllowGet);
        }


        // recupera datos del comerciante
        public JsonResult ObtenerComerciante(int Id)
        {
            Comerciantes oComerciante = new Comerciantes();
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                var resultado = (from comer in db.Comerciantes
                                where comer.Id == Id
                                select comer).ToList().First();

                oComerciante.Id = resultado.Id;
                oComerciante.Nombres = resultado.Nombres;
                oComerciante.Apellidos = resultado.Apellidos;
                oComerciante.Cedula = resultado.Cedula;
                oComerciante.Capacitacion = resultado.Capacitacion;
                oComerciante.instituciones_id = resultado.instituciones_id;
            }
            return Json(oComerciante, JsonRequestBehavior.AllowGet);
        }

        // guardar cambios realizados por ADMINISTRADOR
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public JsonResult Guardar(Comerciantes oComerciante)
        {
            bool respuesta = false;
            string mensaje = String.Empty;
            try
            {
                if (oComerciante.Id == 0)
                {
                    using (ComerciantesEntities db = new ComerciantesEntities())
                    {
                        db.Comerciantes.Add(oComerciante);
                        db.SaveChanges();
                    }
                }
                else
                {
                    // verifica que no tenga certificado generado
                    if (GetCertificadoByComercianteId(oComerciante.Id) != null)
                    {
                        mensaje = "No se guardaron los cambios porque el comerciante ya generó su certificado.";
                    }
                    else
                    {
                        // verifica que campos no estén vacíos
                        if (String.IsNullOrWhiteSpace(oComerciante.Cedula) || String.IsNullOrWhiteSpace(oComerciante.Apellidos) ||
                            String.IsNullOrWhiteSpace(oComerciante.Nombres) || String.IsNullOrWhiteSpace(oComerciante.Capacitacion.ToString()))
                        {
                            mensaje = "No se guardaron los cambios porque existen campos vacíos.";
                        }
                        else
                        {
                            if (oComerciante.Cedula.Length != 10)
                            {
                                mensaje = "No se guardaron los cambios porque el campo Cédula no tiene diez dígitos.";
                            }
                            else
                            {
                                // verifica que campos nombres y apellidos no contengas números
                                if (TextoContieneNumeros(oComerciante.Nombres.Trim()) || TextoContieneNumeros(oComerciante.Apellidos.Trim()))
                                {
                                    mensaje = "No se guardaron los cambios porque los campos Nombres y/o Apellidos contienen números.";
                                }
                                else
                                {
                                    // verifica que no exista número de cédula
                                    if (ExisteCedulaRegistrada(oComerciante))
                                    {
                                        mensaje = "No se guardaron los cambios porque el número de cédula ya existe.";
                                    }
                                    else
                                    {
                                        // verifica que año se no sea menor a 1970 o mayor al actua;
                                        if (oComerciante.Capacitacion <= 2000 || oComerciante.Capacitacion >= DateTime.Today.Year)
                                        {
                                            mensaje = "No se guardaron los cambios porque el año debe estar entre 2000 y "
                                                + DateTime.Today.Year.ToString();
                                        }
                                        else
                                        {
                                            using (ComerciantesEntities db = new ComerciantesEntities())
                                            {
                                                // actualizar datos comerciante
                                                Comerciantes tempComerciante = (from p in db.Comerciantes
                                                                                where p.Id == oComerciante.Id
                                                                                select p).FirstOrDefault();
                                                tempComerciante.Nombres = oComerciante.Nombres.Trim().ToUpper();
                                                tempComerciante.Apellidos = oComerciante.Apellidos.Trim().ToUpper();
                                                tempComerciante.Capacitacion = oComerciante.Capacitacion;
                                                tempComerciante.Cedula = oComerciante.Cedula;
                                                tempComerciante.instituciones_id = oComerciante.instituciones_id;

                                                db.SaveChanges();
                                                respuesta = true;
                                                mensaje = "Se guardaron correctamente los cambios.";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                mensaje = "No se guardaron los cambios porque hubo un error al hacerlo. Inténtelo más tarde.";
            }
            return Json(new { resultado = respuesta, message = mensaje }, JsonRequestBehavior.AllowGet);
        }


        // guarda cambios realizados por TECNICO EN RECTIFICACIONES
        [Authorize(Roles = "Tecnico")]
        [HttpPost]
        public JsonResult GuardarAtencion(Comerciantes oComerciante)
        {
            bool respuesta = false;
            string mensaje = String.Empty;
            try
            {
                if (oComerciante.Id == 0)
                {
                    using (ComerciantesEntities db = new ComerciantesEntities())
                    {
                        db.Comerciantes.Add(oComerciante);
                        db.SaveChanges();
                    }
                }
                else
                {
                    // verifica que campos no estén vacíos
                    if (String.IsNullOrWhiteSpace(oComerciante.Cedula) || String.IsNullOrWhiteSpace(oComerciante.Apellidos) || 
                        String.IsNullOrWhiteSpace(oComerciante.Nombres))
                    {
                        mensaje = "No se guardaron los cambios porque existen campos vacíos";
                    }
                    else
                    {
                        if (oComerciante.Cedula.Length != 10)
                        {
                            mensaje = "No se guardaron los cambios porque el campo Cédula no tiene diez dígitos.";
                        }
                        else
                        {
                            // verifica que campos nombres y apellidos no contengas números
                            if (TextoContieneNumeros(oComerciante.Nombres.Trim()) || TextoContieneNumeros(oComerciante.Apellidos.Trim()))
                            {
                                mensaje = "No se guardaron los cambios porque los campos Nombres y/o Apellidos contienen números.";
                            }
                            else
                            {
                                // verifica que no exista número de cédula
                                if (ExisteCedulaRegistrada(oComerciante))
                                {
                                    mensaje = "No se guardaron los cambios porque el número de cédula ya existe.";
                                }
                                else
                                {
                                    using (ComerciantesEntities db = new ComerciantesEntities())
                                    {
                                        // actualizar datos comerciante
                                        Comerciantes tempComerciante = (from p in db.Comerciantes
                                                                        where p.Id == oComerciante.Id
                                                                        select p).FirstOrDefault();
                                        tempComerciante.Nombres = oComerciante.Nombres.Trim().ToUpper();
                                        tempComerciante.Apellidos = oComerciante.Apellidos.Trim().ToUpper();
                                        tempComerciante.Cedula = oComerciante.Cedula;
                                        tempComerciante.instituciones_id = oComerciante.instituciones_id;

                                        // actualizar atención de rectificación
                                        Rectificaciones tempRectificacion = (from r in db.Rectificaciones
                                                                             where r.comerciantes_id == oComerciante.Id
                                                                             select r).FirstOrDefault();
                                        tempRectificacion.solicitud_atendida = true;

                                        db.SaveChanges();
                                        respuesta = true;
                                        mensaje = "Se guardaron correctamente los cambios.";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                mensaje = "No se guardaron los cambios porque hubo un error al hacerlo. Inténtelo más tarde.";
            }
            return Json(new { resultado = respuesta, message = mensaje }, JsonRequestBehavior.AllowGet);
        }


        public JsonResult Eliminar(int ID)
        {
            bool respuesta = true;
            try
            {
                using (ComerciantesEntities db = new ComerciantesEntities())
                {
                    Comerciantes oComerciante = new Comerciantes();
                    oComerciante = (from p in db.Comerciantes.Where(x => x.Id == ID)
                                    select p).FirstOrDefault();

                    db.Comerciantes.Remove(oComerciante);
                    db.SaveChanges();
                }
            }
            catch
            {
                respuesta = false;
            }

            return Json(new { resultado = respuesta }, JsonRequestBehavior.AllowGet);
        }


        // descarga individual de certificado de capacitación
        public ActionResult VerCC(string rutaArchivo)
        {
            try
            {
                string pathDescarga = Server.MapPath("~/Documentos/CC/");
                byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + rutaArchivo);
                return File(fileBytes, "application/pdf", rutaArchivo);
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                ViewBag.MensajeError = "Error: " + fileNotFoundException.Message.ToString();
                return View("ReporteError");
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al intentar descargar el documento.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }


        // descarga individual de solicitud de rectificación
        public ActionResult VerRect(string rutaArchivo)
        {
            try
            {
                string pathDescarga = Server.MapPath("~/Documentos/Rect/");
                byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + rutaArchivo);
                return File(fileBytes, "application/pdf", rutaArchivo);
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                ViewBag.MensajeError = "Error: " + fileNotFoundException.Message.ToString();
                return View("ReporteError");
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al intentar descargar el documento.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }


        // verifica que cédula a guardar no esté repetida
        public bool ExisteCedulaRegistrada(Comerciantes comerciante)
        {
            bool resultado = false;
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                resultado = (from cert in db.Comerciantes
                             where cert.Cedula == comerciante.Cedula && cert.Id != comerciante.Id
                             select cert).Any();
            }
            return resultado;
        }

        public bool BuscarCertificadoByComercianteId(int comerId)
        {
            bool resultado = false;
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                resultado = (from cert in db.Certificados
                             where cert.comerciantes_id == comerId
                             select cert).Any();
            }
            return resultado;
        }

        public Models.Certificados GetCertificadoByComercianteId(int comerId)
        {
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                var resultado = from cert in db.Certificados
                                where cert.comerciantes_id == comerId && cert.certificado_valido == true
                                select cert;
                return resultado.Any() ? resultado.First() : null;
            }
        }

        public bool BuscarRectificacionByComercianteId(int comerId)
        {
            bool resultado = false;
            try
            {
                using (ComerciantesEntities db = new ComerciantesEntities())
                {
                    resultado = (from rect in db.Rectificaciones
                                 where rect.comerciantes_id == comerId
                                 select rect).Any();
                }
                return resultado;
            }
            catch (Exception)
            {
                return resultado;
            }
        }

        public Rectificaciones GetRectificacionByComercianteId(int comerId)
        {
            using (ComerciantesEntities db = new ComerciantesEntities())
            {
                var resultado = (from rect in db.Rectificaciones
                                 where rect.comerciantes_id == comerId
                                 select rect);
                return resultado.Any() ? resultado.First() : null;
            }
        }

        public bool TextoContieneNumeros(string texto)
        {
            bool resultado = false;
            char[] _charArray = texto.ToCharArray();
            foreach (char item in _charArray)
            {
                if (int.TryParse(item.ToString(), out int value))
                {
                    resultado = true;
                    break;
                }
            }
            return resultado;
        }

    }
}