using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Certificados.Models;
using Certificados.Models.ViewModel;
using Certificados.LogHistorial;

namespace Certificados.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RegistrosController : Controller
    {
        private readonly ComerciantesEntities dbContextRegistros = new ComerciantesEntities();
        public Log log = new Log();
        
        public ComerciantesEntities DbContextRegistros => dbContextRegistros;

        // GET: Registros - ADMIN
        public ActionResult Index()
        {
            //return View(GetComerciantesList());

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


        public ActionResult SubirArchivo()
        {
            ViewBag.Observaciones = false;
            return View();
        }
        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CargarDatosDCA(HttpPostedFileBase file)
        {
            // para mensajes de validación
            ViewBag.Mensaje = String.Empty;
            ViewBag.Observaciones = false;

            // instanciación de variables iniciales
            List<string> listaObservacionesCSV = new List<string>();
            List<Comerciantes> registrosGuardados = new List<Comerciantes>();
            try
            {
                // verifica que se haya subido un archivo y no está vacío
                if (file != null && file.ContentLength > 0)
                {
                    // verificar que sea archivo .CSV
                    String fileExt = Path.GetExtension(file.FileName).ToUpper();
                    if (fileExt == ".CSV")
                    {
                        // guardar archivo CSV - temporal
                        string nombreArchivoCSV = Path.GetFileName(file.FileName).Trim().Replace(" ", "_");
                        string pathArchivoCSV = Path.Combine(Server.MapPath("~/Temporal/"), nombreArchivoCSV);
                        file.SaveAs(pathArchivoCSV);

                        // lectura de datos del archivo CSV
                        List<Comerciantes> listaComerciantesRegistrar = new List<Comerciantes>();
                        string csvDataTexto = System.IO.File.ReadAllText(pathArchivoCSV, Encoding.UTF8).Trim();

                        // verifica que el archivo no esté vacío
                        if (!String.IsNullOrWhiteSpace(csvDataTexto))
                        {
                            string[] csvDataArray = csvDataTexto.Replace("\r", "").Split('\n');

                            // si último elemento de array está vacío, lo elimina
                            if (String.IsNullOrWhiteSpace(csvDataArray.Last()))
                            {
                                csvDataArray = csvDataArray.Take(csvDataArray.Count() - 1).ToArray();
                            }

                            // recorre array pasando la primera fila que contiene los headers
                            for (int i = 1; i < csvDataArray.Length; i++)
                            {
                                // verifica que la fila no esté vacía
                                if (!String.IsNullOrWhiteSpace(csvDataArray[i]))
                                {
                                    string[] csvDataFila = csvDataArray[i].Split(',');
                                    List<string> listaObservacionesFila = VerificarRegistroComeriantes(csvDataFila);

                                    // verifica si hay observaciones que impidan guardar el registro
                                    if (listaObservacionesFila.Count() == 0)
                                    {
                                        listaComerciantesRegistrar.Add(new Comerciantes()
                                        {
                                            Nombres = csvDataFila[0].Trim().ToUpper(),
                                            Apellidos = csvDataFila[1].Trim().ToUpper(),
                                            Cedula = csvDataFila[2].Trim(),
                                            Capacitacion = ConvertirInt(csvDataFila[3].Trim()),
                                            instituciones_id = ConvertirInt(csvDataFila[4].Trim())
                                        });
                                    }
                                    else
                                    {
                                        listaObservacionesCSV.Add("Fila " + (i + 1).ToString() + ": " + String.Join("; ", listaObservacionesFila));
                                    }
                                }
                                else
                                {
                                    listaObservacionesCSV.Add("Fila " + (i + 1).ToString() + " está vacía.");
                                }
                            }
                        }
                        else
                        {
                            log.Info(new Log_Registros()
                            {
                                mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Sube archivo vacío",
                                usuario_log = User.Identity.Name
                            });

                            ViewBag.Mensaje = "El archivo " + nombreArchivoCSV + " está vacío.";
                            return View("SubirArchivo");
                        }

                        // eliminar archivo CVS cargado
                        EliminarArchivoTemporal(pathArchivoCSV);

                        // verificar que la lista a registrar no esté vacía
                        if (listaComerciantesRegistrar.Count != 0)
                        {
                            // verificar cédulas repetidas en lista a guardar
                            List<string> listaCedulasRepetidas = BuscarCedulasRepetidas(listaComerciantesRegistrar);
                            if (listaCedulasRepetidas.Count() > 0)
                            {
                                foreach (var item in listaCedulasRepetidas)
                                {
                                    listaObservacionesCSV.Add("El valor del campo CEDULA '" + item + "' está repetido en los registros a cargar.");
                                }
                            }

                            if (listaObservacionesCSV.Count() == 0 && listaComerciantesRegistrar.Count > 0)
                            {
                                // guardar en tabla COMERCIANTES
                                foreach (Comerciantes comer in listaComerciantesRegistrar)
                                {
                                    dbContextRegistros.Comerciantes.Add(comer);
                                    dbContextRegistros.SaveChanges();
                                    registrosGuardados.Add(GetComercianteById(comer.Id));
                                }

                                // guardar el log
                                log.Transaction(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Carga exitosa de registros en base de datos",
                                    usuario_log = User.Identity.Name
                                });
                                // respuesta a Vista
                                ViewBag.CantidadRegistros = registrosGuardados.Count();
                                return View();
                            }
                            else if (listaObservacionesCSV.Count() == 0 && listaComerciantesRegistrar.Count == 0)
                            {
                                log.Info(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Sube archivo vacío",
                                    usuario_log = User.Identity.Name
                                });
                                ViewBag.Mensaje = "El archivo cargado está vacío.";
                                ViewData["listaObservaciones"] = listaObservacionesCSV;
                                return View("SubirArchivo");
                            }
                            else
                            {
                                log.Info(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Sube archivo con errores",
                                    usuario_log = User.Identity.Name
                                });
                                ViewBag.Observaciones = true;
                                ViewBag.Mensaje = "No se puede cargar el archivo porque contiene los siguientes errores.";
                                ViewData["listaObservaciones"] = listaObservacionesCSV;
                                return View("SubirArchivo");
                            }
                        }
                        else
                        {
                            if (listaObservacionesCSV.Count() != 0)
                            {
                                log.Info(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Sube archivo con errores",
                                    usuario_log = User.Identity.Name
                                });

                                ViewBag.Observaciones = true;
                                ViewBag.Mensaje = "No se puede cargar el archivo \'" + file.FileName + "\' porque contiene los siguientes errores.";
                                ViewData["listaObservaciones"] = listaObservacionesCSV;
                                return View("SubirArchivo");
                            }
                            else
                            {
                                log.Info(new Log_Registros()
                                {
                                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Sube archivo sin registros",
                                    usuario_log = User.Identity.Name
                                });
                                ViewBag.Mensaje = "El archivo cargado no contiene registros.";
                                return View("SubirArchivo");
                            }
                        }
                    }
                    else
                    {
                        log.Info(new Log_Registros()
                        {
                            mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Intenta subir archivo que no es CSV",
                            usuario_log = User.Identity.Name
                        });                        
                        ViewBag.Mensaje = "El archivo cargado no tiene extensión CSV.";
                        return View("SubirArchivo");
                    }
                }
                else
                {
                    log.Info(new Log_Registros()
                    {
                        mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "No carga archivo con registros de comerciantes",
                        usuario_log = User.Identity.Name
                    });                    
                    ViewBag.Mensaje = "Para subir los registros, debe cargar un archivo en formato CSV"; 
                    return View("SubirArchivo");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Log_Registros()
                {
                    mensaje_log = RouteData.Values["action"] + " en " + RouteData.Values["controller"].ToString() + "Controller: " + "Error al cargar registros de comerciantes",
                    usuario_log = User.Identity.Name,
                    excepcion_log = ex.Message.ToString() + " - " + ex.StackTrace.ToString()
                });
                ViewBag.Mensaje = "Hubo un error al momento de cargar los datos. Inténtelo más tarde.";
                return View("SubirArchivo");
            }
        }


        // métodos soporte --------------------------------------------------------------------------------

        public Comerciantes GetComercianteById(int comerId)
        {
            var resultado = from c in dbContextRegistros.Comerciantes
                              where c.Id == comerId
                              select c;
            return resultado.Any() ? resultado.First() : null;
        }

        public List<Comerciantes> GetComerciantesList()
        {
            var resultado = from c in dbContextRegistros.Comerciantes
                            select c;
            return resultado.Any() ? resultado.ToList() : null;
        }

        public int ConvertirInt(string datoString)
        {
            Int32.TryParse(datoString, out int datoInt);
            return datoInt;
        }

        public void EliminarArchivoTemporal(string _path)
        {
            if (System.IO.File.Exists(_path))
            {
                System.IO.File.Delete(_path);
            }
        }

        public string RetirarDigitosDeTexto(string texto)
        {
            string resultado = String.Empty;
            char[] _charArray = texto.ToCharArray();
            foreach (char item in _charArray)
            {
                if (int.TryParse(item.ToString(), out _))
                {
                    continue;
                }
                resultado.Append(item);
            }
            return resultado;
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

        public bool TextoContieneLetras(string texto)
        {
            bool resultado = false;
            char[] _charArray = texto.ToCharArray();
            foreach (char item in _charArray)
            {
                if (Char.IsLetter(item))
                {
                    resultado = true;
                    break;
                }
            }
            return resultado;
        }

        public bool TextoContieneEspacios(string texto)
        {
            return texto.Any(char.IsWhiteSpace);
        }

        public List<string> BuscarCedulasRepetidas(List<Comerciantes> listaComeciantes)
        {
            List<string> listaResultado = new List<string>();
            foreach (var comer in listaComeciantes)
            {
                if (!listaResultado.Contains(comer.Cedula))
                {
                    var resultado = (from c in listaComeciantes
                                     where c.Cedula == comer.Cedula
                                     select c).ToList();
                    if (resultado.Count() > 1)
                    {

                        listaResultado.Add(comer.Cedula);
                    }
                }                
            }
            return listaResultado;
        }

        public int ExisteCedulaComerciante(string cedulaDato)
        {
            var resultado = from comer in dbContextRegistros.Comerciantes
                            where comer.Cedula == cedulaDato
                            select comer;
            return resultado.Any() ? resultado.Count() : 0;
        }

        public int ValidarCapacitacionComerciante(string capacitacionDato)
        {
            // return: anio, si está correcto; 0, si no pudo convertir; -1, si anio es menor a 1970 o mayor que el actual
            if (Int32.TryParse(capacitacionDato, out int valor))
            {
                if (valor >= 1970 && valor <= DateTime.Now.Year)
                {
                    return valor;
                }
                return -1;
            }
            else
            {
                return 0;
            }
        }

        public int ValidarInstitucionComerciante(string institucionIdDato)
        {
            // return: institucionId, si está correcto; 0, si no pudo convertir; y -1, si tiene 0 ó más de 1 dígitos, excepto el 10
            if (Int32.TryParse(institucionIdDato, out int valor))
            {
                if (valor >= 1 && valor <= dbContextRegistros.Institucion.Count())
                {
                    return valor;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return 0;
            }
        }

        public List<string> VerificarRegistroComeriantes(string[] datosFilaCSV)
        {
            List<string> resultadoValidacion = new List<string>();
            
            // validación NOMBRES
            if (!String.IsNullOrWhiteSpace(datosFilaCSV[0].Trim()))
            {
                if (TextoContieneNumeros(datosFilaCSV[0].Trim()))
                {
                    resultadoValidacion.Add("El campo NOMBRES contiene dígitos");
                }
            }
            else
            {
                resultadoValidacion.Add("El campo NOMBRES está vacío");
            }

            // validación APELLIDOS
            if (!String.IsNullOrWhiteSpace(datosFilaCSV[1].Trim()))
            {
                if (TextoContieneNumeros(datosFilaCSV[1].Trim()))
                {
                    resultadoValidacion.Add("El campo APELLIDOS contiene dígitos");
                }
            }
            else
            {
                resultadoValidacion.Add("El campo APELLIDOS está vacío");
            }

            // validación CEDULA
            if (!String.IsNullOrWhiteSpace(datosFilaCSV[2].Trim()))
            {
                if (!TextoContieneEspacios(datosFilaCSV[2].Trim()))
                {
                    if (datosFilaCSV[2].Trim().Length == 10)
                    {
                        if (TextoContieneLetras(datosFilaCSV[2].Trim()))
                        {
                            resultadoValidacion.Add("El valor del campo CEDULA, contiene letras");
                        }
                        else
                        {
                            if (ExisteCedulaComerciante(datosFilaCSV[2].Trim()) != 0)
                            {
                                resultadoValidacion.Add("El valor del campo CEDULA, ya existe en los registros");
                            }
                        }
                    }
                    else
                    {
                        resultadoValidacion.Add("El valor del campo CEDULA debe tener diez dígitos");
                    }
                }
                else
                {
                    resultadoValidacion.Add("El campo CEDULA contiene espacios en blanco");
                }
            }
            else
            {
                resultadoValidacion.Add("El campo CEDULA está vacío");
            }

            // validación año capacitación
            // return: anio, si está correcto; 0, si no pudo convertir; -1, si anio es menor a 1970 o mayor que el actual
            if (TextoContieneEspacios(datosFilaCSV[3].Trim()))
            {
                resultadoValidacion.Add("El campo CAPACITACION contiene espacios en blanco");
            }
            else
            {
                if (!String.IsNullOrWhiteSpace(datosFilaCSV[3].Trim()))
                {
                    switch (ValidarCapacitacionComerciante(datosFilaCSV[3].Trim()))
                    {
                        case 0:
                            resultadoValidacion.Add("El valor del campo CAPACITACION contiene letras");
                            break;
                        case -1:
                            resultadoValidacion.Add("El valor del campo CAPACITACION es menor a 1970 o mayor al año actual");
                            break;
                    }
                }
                else
                {
                    resultadoValidacion.Add("El campo CAPACITACION está vacío");
                }
            }

            // validación institucion (id)
            // return: institucionId, si está correcto; 0, si no pudo convertir; y -1, si tiene 0 ó más de 1 dígitos, excepto el 10
            if (TextoContieneEspacios(datosFilaCSV[3].Trim()))
            {
                resultadoValidacion.Add("El campo INSTITUCIONES_ID contiene espacios en blanco");
            }
            else
            {
                if (!String.IsNullOrWhiteSpace(datosFilaCSV[4].Trim()))
                {
                    switch (ValidarInstitucionComerciante(datosFilaCSV[4].Trim()))
                    {
                        case 0:
                            resultadoValidacion.Add("El valor del campo INSTITUCIONES_ID contiene letras");
                            break;
                        case -1:
                            resultadoValidacion.Add("El valor del campo INSTITUCIONES_ID no corresponde con alguna institución");
                            break;
                    }
                }
                else
                {
                    resultadoValidacion.Add("El campo INSTITUCIONES_ID está vacío");
                }
            }            
            
            return resultadoValidacion;
        }
    }
}