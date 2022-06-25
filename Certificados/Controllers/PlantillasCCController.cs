using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Certificados.Models;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;

namespace Certificados.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PlantillasCCController : Controller
    {
        private readonly ComerciantesEntities dbContextPlantillas = new ComerciantesEntities();

        public ComerciantesEntities DbContextPlantillas => dbContextPlantillas;


        // GET: PlantillasCC
        public ActionResult Index()
        {
            return View("InicioPlantillas");
        }


        public ActionResult InicioPlantillas()
        {
            return View();
        }

        public ActionResult VerPlantillaActiva(string documento)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(documento))
                {
                    ViewBag.Tipo = documento;

                    if (GetPlantillaActiva(documento) != null)
                    {
                        return View(GetPlantillaActiva(documento));
                    }
                    else
                    {
                        return View("NoPlantillaActiva");
                    }
                }
                else
                {
                    return View("InicioPlantillas");
                }
            }
            catch (Exception)
            {
                ViewBag.Mensaje = "Ha ocurrido un error al intentar recuperar la plantilla. Por favor inténtelo más tarde.";
                return View("NoPlantillaActiva");
            }
        }


        public ActionResult EstablecerPlantilla(string documento)        // revisar se pasa el id de la plantilla
        {
            ViewBag.Tipo = documento;
            return View(new PlantillasCC() { documento_plantilla = documento } );
        }

                
        public ActionResult VerArchivoPlantilla(string nombreArchivo)         // permite ver el archivo pdf de la plantilla
        {
            try
            {
                string pathDescarga = Server.MapPath("~/PlantillasCC/");
                byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + nombreArchivo);
                return File(fileBytes, "application/pdf", nombreArchivo);
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                ViewBag.MensajeError = "Error: " + fileNotFoundException.Message.ToString();
                return View("ReporteError");
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al intentar descargar la plantilla.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }


        [HttpPost]
        public ActionResult CargarPlantilla(PlantillasCC plantilla, HttpPostedFileBase file)   // registra plantilla en DB
        {
            try
            {
                // para mensajes de validación en vista
                ViewBag.Mensaje = String.Empty;
                ViewBag.Tipo = String.Empty;

                // verifica que nombre_plantilla no esté vacío
                if (!String.IsNullOrWhiteSpace(plantilla.nombre_plantilla))
                {
                    // quitar espacios en blanco
                    plantilla.nombre_plantilla = plantilla.nombre_plantilla.Trim().Replace(" ", "_");

                    // verifica que nombre no sea repetido
                    if (!BuscarNombrePlantillaCC(plantilla.nombre_plantilla))
                    {
                        // verifica que el archivo exista y tenga extensión .PDF
                        if (file != null && file.ContentLength > 0)
                        {
                            // verificar que el archivo tenga extensión .PDF
                            string nombreArchivoPlantilla = Path.GetFileName(file.FileName).Trim().Replace(" ", "_");
                            String fileExt = Path.GetExtension(file.FileName).ToUpper();
                            if (fileExt == ".PDF")
                            {
                                // revisa que el nombre del archivo no conincida con uno existente, antes de guardarlo
                                var listaArchivosRevisar = GetArchivosPlantillasGuardadas();
                                if (listaArchivosRevisar != null)
                                {
                                    if (listaArchivosRevisar.Contains(nombreArchivoPlantilla))
                                    {
                                        ViewBag.Mensaje = "Existe otro archivo con el mismo nombre. " +
                                            "Para guardar la plantilla, modifique el nombre del archivo.";
                                        ViewBag.Tipo = plantilla.documento_plantilla;
                                        return View("EstablecerPlantilla");
                                    }
                                }

                                // guardar el archivo
                                plantilla.archivo_plantilla = nombreArchivoPlantilla;
                                string _path = Path.Combine(Server.MapPath("~/PlantillasCC/"), nombreArchivoPlantilla);
                                file.SaveAs(_path);

                                // validar campos plantilla y archivo cargado
                                var pdfReader = new PdfReader(_path);
                                PdfDocument pdf = new PdfDocument(pdfReader);
                                PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                                IDictionary<String, PdfFormField> fields = form.GetFormFields();

                                // validar campos como certificado
                                List<string> listaObservaciones = new List<string>();
                                if (plantilla.documento_plantilla == "certificado")
                                {
                                    listaObservaciones = ValidarCamposPlantillaCC(fields);
                                    if (listaObservaciones.Count() != 0)
                                    {
                                        pdf.Close();
                                        pdfReader.Close();
                                        BorrarArchivo(_path);
                                        ViewBag.mensaje = "No se pudo guardar la plantilla porque no tiene los campos: " +
                                            String.Join("; ", listaObservaciones);
                                        ViewBag.Tipo = plantilla.documento_plantilla;
                                        return View("EstablecerPlantilla");
                                    }
                                }
                                else if (plantilla.documento_plantilla == "rectificacion")  // validar campos como rectificacion
                                {
                                    listaObservaciones = ValidarCamposPlantillaRect(fields);
                                    if (listaObservaciones.Count() != 0)
                                    {
                                        pdf.Close();
                                        pdfReader.Close();
                                        BorrarArchivo(_path);
                                        ViewBag.mensaje = "No se pudo guardar la plantilla porque no tiene los campos: " +
                                            String.Join("; ", listaObservaciones);
                                        ViewBag.Tipo = plantilla.documento_plantilla;
                                        return View("EstablecerPlantilla");
                                    }
                                }

                                // asignar valores (documento_plantilla ya está asignado)
                                plantilla.fecha_plantilla = DateTime.Now;
                                //plantilla.nombre_plantilla = plantilla.nombre_plantilla.Trim();
                                plantilla.plantilla_activa = true;
                                plantilla.archivo_plantilla = nombreArchivoPlantilla;

                                // desactivar plantilla activa actual
                                PlantillasCC plantillaActiva = GetPlantillaActiva(plantilla.documento_plantilla);
                                if (plantillaActiva != null)
                                {
                                    plantillaActiva = dbContextPlantillas.PlantillasCC.Find(plantillaActiva.Id);
                                    plantillaActiva.plantilla_activa = false;
                                    dbContextPlantillas.Entry(plantillaActiva).State = System.Data.Entity.EntityState.Modified;
                                }

                                // guardar plantillaCC
                                dbContextPlantillas.PlantillasCC.Add(plantilla);
                                if (dbContextPlantillas.SaveChanges() > 0)
                                {
                                    ViewBag.Tipo = plantilla.documento_plantilla;
                                    ViewBag.Mensaje = "La plantilla se creó correctamente.";
                                    return View(GetPlantillaById(plantilla.Id));
                                }
                                else
                                {
                                    ViewBag.Mensaje = "No se pudo guardar la plantilla";
                                    return View("EstablecerPlantilla");
                                }
                            }
                            else
                            {
                                ViewBag.Tipo = plantilla.documento_plantilla;
                                ViewBag.Mensaje = "El archivo cargado no tiene extensión PDF.";
                                return View("EstablecerPlantilla");
                            }                            
                        }
                        else
                        {
                            ViewBag.Mensaje = "Para crear la plantilla, debe subir un archivo en formato PDF.";
                            ViewBag.Tipo = plantilla.documento_plantilla;
                            return View("EstablecerPlantilla");
                        }
                    }
                    else
                    {
                        ViewBag.Tipo = plantilla.documento_plantilla;
                        ViewBag.Mensaje = "El nombre " + plantilla.nombre_plantilla + " ya existe, escriba otro.";
                        return View("EstablecerPlantilla");
                    }
                }
                else
                {
                    ViewBag.Tipo = plantilla.documento_plantilla;
                    ViewBag.Mensaje = "Para crear la plantilla, debe asignarle un nombre.";
                    return View("EstablecerPlantilla");
                }
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al momento de guardar la plantilla.";
                ViewBag.MensajeError = "Error al intentar guardar la plantilla" + e.Message.ToString();
                return View("ReporteError");
            }
        }


        public PlantillasCC GetPlantillaActiva(string documento)
        {
            var resultado = from pcc in dbContextPlantillas.PlantillasCC
                            where pcc.documento_plantilla == documento && pcc.plantilla_activa == true
                            select pcc;
            return resultado.Any() ? resultado.First() : null;
        }


        public List<string> GetArchivosPlantillasGuardadas()
        {
            var resultado = from pcc in dbContextPlantillas.PlantillasCC
                            select pcc.archivo_plantilla;
            return resultado.Any() ? resultado.ToList() : null;
        }


        public PlantillasCC GetPlantillaById(int id)
        {
            var resultado = from pcc in dbContextPlantillas.PlantillasCC
                            where pcc.Id == id
                            select pcc;
            return resultado.Any() ? resultado.First() : null;
        }

        public PlantillasCC GetPlantillaByTipoDocumento(string documento)
        {
            var resultado = from pcc in dbContextPlantillas.PlantillasCC
                            where pcc.documento_plantilla == documento && pcc.plantilla_activa == true
                            select pcc;
            return resultado.Any() ? resultado.First() : null;
        }

        public bool BuscarNombrePlantillaCC(string nombrePlantilla)
        {
            var resultado = from pcc in DbContextPlantillas.PlantillasCC
                            where pcc.nombre_plantilla == nombrePlantilla
                            select pcc;
            return resultado.Any();
        }

        public void BorrarArchivo(string path)
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        public List<String> ValidarCamposPlantillaCC(IDictionary<String, PdfFormField> fields)
        {
            List<string> listaResultado = new List<string>();

            if (!fields.ContainsKey("numero_doc"))
            {
                listaResultado.Add("'numero_doc'");
            }
            if (!fields.ContainsKey("fecha_doc"))
            {
                listaResultado.Add("'fecha_doc'");
            }
            if (!fields.ContainsKey("nombres_apellidos"))
            {
                listaResultado.Add("'nombres_apellidos'");
            }
            if (!fields.ContainsKey("cedula"))
            {
                listaResultado.Add("'cedula'");
            }
            if (!fields.ContainsKey("capacitacion"))
            {
                listaResultado.Add("'capacitacion'");
            }
            if (!fields.ContainsKey("anio_cc"))
            {
                listaResultado.Add("'anio_cc'");
            }
            if (!fields.ContainsKey("institucion"))
            {
                listaResultado.Add("'institucion'");
            }
            if (!fields.ContainsKey("directorDCA"))
            {
                listaResultado.Add("'directorDCA'");
            }

            return listaResultado;
        }

        public List<String> ValidarCamposPlantillaRect(IDictionary<String, PdfFormField> fields)
        {
            List<string> listaResultado = new List<string>();

            if (!fields.ContainsKey("numero_rect"))
            {
                listaResultado.Add("'numero_rect'");
            }
            if (!fields.ContainsKey("fecha_rect"))
            {
                listaResultado.Add("'fecha_rect'");
            }
            if (!fields.ContainsKey("coordinadorACDC"))
            {
                listaResultado.Add("'coordinadorACDC'");
            }
            if (!fields.ContainsKey("datos_rectificar"))
            {
                listaResultado.Add("'datos_rectificar'");
            }

            return listaResultado;
        }
    }
}