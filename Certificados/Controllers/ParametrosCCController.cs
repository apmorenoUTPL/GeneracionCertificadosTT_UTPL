using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Certificados.LogHistorial;
using Certificados.Models;

namespace Certificados.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ParametrosCCController : Controller
    {
        private readonly ComerciantesEntities dbContextParametrosCC = new ComerciantesEntities();
        public Log log = new Log();

        public ComerciantesEntities DbContextParametrosCC => dbContextParametrosCC;

        // GET: ParametrosCC
        public ActionResult Index()
        {
            return View(GetParametrosCC());
        }

        public ActionResult EditarParametrosCC(int parametroId)
        {
            return View(GetParametrosCCById(parametroId));
        }

        public ActionResult GuardarParametrosCC(ParametrosCC param)
        {
            try
            {
                ViewBag.Mensaje = String.Empty;
                if (!String.IsNullOrWhiteSpace(param.valor_parametro))
                {
                    ParametrosCC paramGuardar = dbContextParametrosCC.ParametrosCC.Find(param.Id);
                    paramGuardar.valor_parametro = param.valor_parametro;
                    dbContextParametrosCC.Entry(paramGuardar).State = System.Data.Entity.EntityState.Modified;

                    if (dbContextParametrosCC.SaveChanges() > 0)
                    {
                        log.Transaction(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Se guardaron los cambios del Parámetro id=" + param.Id,
                            usuario_log = User.Identity.Name
                        });
                        ViewBag.Mensaje = "Se ha registrado exitosamente la modificación del parámetro.";
                        return View(GetParametrosCCById(paramGuardar.Id));
                    }
                    else
                    {
                        log.Warning(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No se pudo guardar cambios de Parámetro id=" + param.Id,
                            usuario_log = User.Identity.Name
                        }); 
                        ViewBag.Mensaje = "No se pudo editar el parámetro. Por favor, inténtelo más tarde.";
                        return View(GetParametrosCCById(param.Id));
                    }
                }
                else
                {
                    ViewBag.Mensaje = "El valor del parámetro no puede estar vacío.";
                    return View("EditarParametrosCC", GetParametrosCCById(param.Id));
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al guardar cambios de Parámetro id=" + param.Id,
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.mensaje = "Hubo un error al editar el parámetro. Por favor, intente más tarde.";
                return View(GetParametrosCCById(param.Id));
            }
        }

        // métodos de soporte ----------------------------------------------------------------

        public List<ParametrosCC> GetParametrosCC()
        {
            var resultado = from param in dbContextParametrosCC.ParametrosCC
                            select param;
            return resultado.Any() ? resultado.ToList() : null;
        }

        public ParametrosCC GetParametrosCCById(int parametroId)
        {
            var resultado = from param in dbContextParametrosCC.ParametrosCC
                            where param.Id == parametroId
                            select param;
            return resultado.Any() ? resultado.First() : null;
        }
    }
}