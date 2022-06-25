using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Certificados.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Kernel.Geom;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Image;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using QRCoder;
using System.IO;
using Certificados.LogHistorial;

namespace Certificados.Controllers
{
    [Authorize]
    public class PlantillaController : Controller
    {
        private readonly ComerciantesEntities dbContextPlantillas = new ComerciantesEntities();
        public Log log = new Log();

        public ComerciantesEntities DbContextPlantillas => dbContextPlantillas;

        // GET: Plantilla
        public ActionResult Index()
        {
            return View("AdministrarPlantillas");
        }

        public ActionResult AdministrarPlantillas()     // administración de plantillas
        {
            return View(GetPlantillasActivas());
        }

        public ActionResult CrearPlantilla()            // creación de un nueva plantilla
        {
            return View(new Plantillas());
        }

        public ActionResult CancelarPlantilla()         // cancela la acción y regresa a Administración
        {
            return RedirectToAction("AdministrarPlantillas");
        }

        public ActionResult VerPlantilla(string nombreArchivoPlantilla)         // permite ver el archivo pdf de la plantilla
        {
            try
            {
                string pathDescarga = Server.MapPath("~/PlantillasOtros/");
                byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + nombreArchivoPlantilla);
                return File(fileBytes, "application/pdf", nombreArchivoPlantilla);
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                ViewBag.Mensaje = "No se ha encontrado el archivo indicado.";
                ViewBag.MensajeError = "Error: " + fileNotFoundException.Message.ToString();
                return View("ReporteError");
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al intentar ver la plantilla.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }

        public ActionResult DetallarPlantilla(int idPlantilla)         // muestra los detalles de la plantilla
        {
            return View(GetPlantillaById(idPlantilla));
        }

        public ActionResult EditarPlantilla(int idPlantilla)         // edita los detalles de la plantilla
        {
            return View(GetPlantillaById(idPlantilla));
        }

        public ActionResult BorrarPlantilla(int idPlantilla)         // borra los detalles de la plantilla
        {
            return View(GetPlantillaById(idPlantilla));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmarBorrado(Plantillas plantilla)         // borra los detalles de la plantilla
        {
            try
            {
                ViewBag.Mensaje = String.Empty;                
                Plantillas plantillaBorrar = new Plantillas();
                using (var dbPlantilla = new ComerciantesEntities())
                {
                    plantillaBorrar = dbPlantilla.Plantillas.Find(plantilla.Id);
                    plantillaBorrar.plantilla_activa = false;
                    dbPlantilla.Entry(plantillaBorrar).State = System.Data.Entity.EntityState.Modified;
                    dbPlantilla.SaveChanges();

                    if (dbPlantilla.SaveChanges() > 0)
                    {
                        ViewBag.Mensaje = "Plantilla eliminada correctamente.";
                        return RedirectToAction("AdministrarPlantillas");
                    }
                    else
                    {
                        ViewBag.Mensaje = "Se ha presentado un problema al intentar eliminar la plantilla.";
                        ViewBag.MensajeError = "Inténtelo más tarde.";
                        return View("ReporteError");
                    }
                }
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al intentar eliminar la plantilla.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CargarPlantilla(Plantillas plantilla, HttpPostedFileBase file)   // registra plantilla en DB
        {
            try
            {
                // bandera para mensaje de validación
                ViewBag.MesajeArchivo = false;

                // verifica que se haya dado un nombre a la plantilla
                if (!String.IsNullOrWhiteSpace(plantilla.name))
                {
                    // quitar espacios en blanco
                    plantilla.name = plantilla.name.Trim().Replace(" ", "_");

                    // verifica que no exista el nombre en la DB
                    if (!BuscarNombrePlantilla(plantilla.name))
                    {
                        // verifica que se haya subido un archivo y tenga contenido
                        if (file != null && file.ContentLength > 0)
                        {
                            String fileExt = System.IO.Path.GetExtension(file.FileName).ToUpper();

                            // verificar que el archivo tenga extensión .PDF
                            if (fileExt == ".PDF")
                            {
                                // guardar archivo
                                string nombreArchivoPlantilla = System.IO.Path.GetFileName(file.FileName).Replace(" ", "_");
                                plantilla.archivo_plantilla = nombreArchivoPlantilla;
                                string _path = System.IO.Path.Combine(Server.MapPath("~/PlantillasOtros/"), nombreArchivoPlantilla);
                                file.SaveAs(_path);

                                // validar campos plantilla y archivo cargado
                                var pdfReader = new PdfReader(_path);
                                PdfDocument pdf = new PdfDocument(pdfReader);
                                PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                                IDictionary<String, PdfFormField> fields = form.GetFormFields();

                                // verificar campos opcionales con plantilla
                                // campo opciona1
                                if (fields.ContainsKey("opcional1") && !plantilla.opcional1_plantilla)
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "el archivo contiene el campo opcional1 pero la plantilla no tiene este campo habilitado";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                if (plantilla.opcional1_plantilla && !fields.ContainsKey("opcional1"))
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "la plantilla tiene el campo opcional1 habilitado pero el archivo no tiene este campo.";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                // campo opcional2
                                if (fields.ContainsKey("opcional2") && !plantilla.opcional2_plantilla)
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "el archivo contiene el campo opcional2 pero la plantilla no tiene este campo habilitado";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                if (plantilla.opcional2_plantilla && !fields.ContainsKey("opcional2"))
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "la plantilla tiene el campo opcional2 habilitado pero el archivo no tiene este campo.";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                // campo opcional3
                                if (fields.ContainsKey("opcional3") && !plantilla.opcional3_plantilla)
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "el archivo contiene el campo opcional3 pero la plantilla no tiene este campo habilitado";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                if (plantilla.opcional3_plantilla && !fields.ContainsKey("opcional3"))
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "la plantilla tiene el campo opcional3 habilitado pero el archivo no tiene este campo.";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                // campo opcional4
                                if (fields.ContainsKey("opcional4") && !plantilla.opcional4_plantilla)
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "el archivo contiene el campo opcional4 pero la plantilla no tiene este campo habilitado";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }
                                if (plantilla.opcional4_plantilla && !fields.ContainsKey("opcional4"))
                                {
                                    ViewBag.Mensaje = "No se pudo crear la plantilla porque " +
                                        "la plantilla tiene el campo opcional4 habilitado pero el archivo no tiene este campo.";
                                    pdf.Close();
                                    pdfReader.Close();
                                    BorrarArchivo(_path);
                                    return View("CrearPlantilla", plantilla);
                                }

                                // si pasa validaciones, asignar valores a PLANTILLA
                                plantilla.fecha_creacion = DateTime.Now;
                                plantilla.nombres_apellidos_plantilla = true;
                                plantilla.curso_taller_plantilla = true;
                                plantilla.fecha_plantilla = true;
                                plantilla.plantilla_activa = true;
                                //plantilla.name = plantilla.name.Trim().Replace(" ", "_");

                                // guardar plantilla
                                dbContextPlantillas.Plantillas.Add(plantilla);
                                if (dbContextPlantillas.SaveChanges() > 0)
                                {
                                    ViewBag.Mensaje = "La plantilla se creó correctamente.";
                                    return View(GetPlantillaById(plantilla.Id));
                                }
                                else
                                {
                                    ViewBag.Mensaje = "No se pudo guardar la plantilla";
                                    return View();
                                }
                            }
                            else
                            {
                                ViewBag.Mensaje = "El archivo cargado no tiene extensión PDF.";
                                return View("CrearPlantilla", plantilla);
                            }                            
                        }
                        else
                        {
                            ViewBag.MesajeArchivo = true;
                            ViewBag.Mensaje = "Para crear la plantilla, debe subir un archivo en formato PDF";
                            return View("CrearPlantilla", plantilla);
                        }
                    }
                    else
                    {
                        ViewBag.Mensaje = "El nombre \'" + plantilla.name + "\' ya existe, escriba otro.";
                        return View("CrearPlantilla", plantilla);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "Para crear la plantilla, debe asignarle un nombre.";
                    return View("CrearPlantilla", plantilla);
                }                
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al momento de registrar la plantilla.";
                ViewBag.MensajeError = "Error al intentar guardar la plantilla" + e.Message.ToString();
                return View("ReporteError");
            }
        }


        public ActionResult GuardarPlantillaEditada()
        {
            return RedirectToAction("AdministrarPlantillas");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarPlantillaEditada(Plantillas plantilla, HttpPostedFileBase file)   // registra plantilla en DB
        {
            try
            {
                Plantillas plantillaEnEdicion = dbContextPlantillas.Plantillas.Find(plantilla.Id);
                
                // verificar que nombre no esté vacío
                if (!String.IsNullOrWhiteSpace(plantilla.name))
                {
                    // quitar espacios en blanco
                    plantilla.name = plantilla.name.Trim().Replace(" ", "_");

                    // verificar si NAME es diferente para asignar
                    if (plantillaEnEdicion.name != plantilla.name)
                    {
                        // verificar que no exista NAME en otra plantill
                        if (!BuscarNombrePlantilla(plantilla.name))
                        {
                            plantillaEnEdicion.name = plantilla.name;
                        }
                        else
                        {
                            ViewBag.Mensaje = "El nombre " + plantilla.name + " ya existe, escriba otro.";
                            return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                        }                        
                    }

                   // verificar cambios en campos opcionales y asignar
                    if (plantillaEnEdicion.opcional1_plantilla != plantilla.opcional1_plantilla)
                    {
                        plantillaEnEdicion.opcional1_plantilla = plantilla.opcional1_plantilla;
                    }
                    if (plantillaEnEdicion.opcional2_plantilla != plantilla.opcional2_plantilla)
                    {
                        plantillaEnEdicion.opcional2_plantilla = plantilla.opcional2_plantilla;
                    }
                    if (plantillaEnEdicion.opcional3_plantilla != plantilla.opcional3_plantilla)
                    {
                        plantillaEnEdicion.opcional3_plantilla = plantilla.opcional3_plantilla;
                    }
                    if (plantillaEnEdicion.opcional4_plantilla != plantilla.opcional4_plantilla)
                    {
                        plantillaEnEdicion.opcional4_plantilla = plantilla.opcional4_plantilla;
                    }

                    // obtener el path del archivo para validación de los campos de la plantilla
                    string _path = System.IO.Path.Combine(Server.MapPath("~/PlantillasOtros/"), plantillaEnEdicion.archivo_plantilla);

                    // revisar si se subió un nuevo archivo para la plantilla
                    if (file != null && file.ContentLength > 0)
                    {
                        // verificar que el archivo tenga extensión .PDF
                        String fileExt = System.IO.Path.GetExtension(file.FileName).ToUpper();
                        if (fileExt == ".PDF")
                        {
                            // guardar el nuevo archivo
                            string nombreArchivoPlantilla = System.IO.Path.GetFileName(file.FileName).Replace(" ", "_");
                            plantillaEnEdicion.archivo_plantilla = nombreArchivoPlantilla;
                            _path = System.IO.Path.Combine(Server.MapPath("~/PlantillasOtros/"), nombreArchivoPlantilla);
                            file.SaveAs(_path);
                        }
                        else
                        {
                            ViewBag.Mensaje = "El archivo cargado no tiene extensión PDF.";
                            return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                        }
                    }

                    // validar campos plantilla y archivo cargado
                    var pdfReader = new PdfReader(_path);
                    PdfDocument pdf = new PdfDocument(pdfReader);
                    PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                    IDictionary<String, PdfFormField> fields = form.GetFormFields();
                    // campo opciona1
                    if (fields.ContainsKey("opcional1") && !plantilla.opcional1_plantilla)
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "el archivo contiene el campo opcional1 pero la plantilla no tiene este campo habilitado";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    if (plantilla.opcional1_plantilla && !fields.ContainsKey("opcional1"))
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "la plantilla tiene el campo opcional1 habilitado pero el archivo no tiene este campo.";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    // campo opcional2
                    if (fields.ContainsKey("opcional2") && !plantilla.opcional2_plantilla)
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "el archivo contiene el campo opcional2 pero la plantilla no tiene este campo habilitado";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    if (plantilla.opcional2_plantilla && !fields.ContainsKey("opcional2"))
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "la plantilla tiene el campo opcional2 habilitado pero el archivo no tiene este campo.";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    // campo opcional3
                    if (fields.ContainsKey("opcional3") && !plantilla.opcional3_plantilla)
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "el archivo contiene el campo opcional3 pero la plantilla no tiene este campo habilitado";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    if (plantilla.opcional3_plantilla && !fields.ContainsKey("opcional3"))
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "la plantilla tiene el campo opcional3 habilitado pero el archivo no tiene este campo.";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    // campo opcional4
                    if (fields.ContainsKey("opcional4") && !plantilla.opcional4_plantilla)
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "el archivo contiene el campo opcional4 pero la plantilla no tiene este campo habilitado";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    if (plantilla.opcional4_plantilla && !fields.ContainsKey("opcional4"))
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios porque " +
                            "la plantilla tiene el campo opcional4 habilitado pero el archivo no tiene este campo.";
                        return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    pdf.Close();
                    pdfReader.Close();

                    // guardar cambios
                    dbContextPlantillas.Entry(plantillaEnEdicion).State = System.Data.Entity.EntityState.Modified;
                    if (dbContextPlantillas.SaveChanges() > 0)
                    {
                        ViewBag.Mensaje = "Se guardaron los cambios correctamente.";
                        return View("CargarPlantilla", GetPlantillaById(plantilla.Id));
                    }
                    else
                    {
                        ViewBag.Mensaje = "No se pudo guardar los cambios. Intente más tarde.";
                        return View();
                    }
                }
                else
                {
                    ViewBag.Mensaje = "Para guardar los cambios, debe asignarle un nombre a la plantilla.";
                    return View("EditarPlantilla", GetPlantillaById(plantilla.Id));
                }                
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha presentado un problema al momento de guardar los cambios.";
                ViewBag.MensajeError = "Error: " + e.Message.ToString();
                return View("ReporteError");
            }
        }


        public ActionResult SeleccionarDatos(Plantillas plantilla)       // seleccionar datos para la plantilla
        {
            ViewBag.Mensaje = String.Empty;
            ViewBag.Observaciones = false;
            return View(plantilla);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CargarDatos(Plantillas plantilla, HttpPostedFileBase file)    // carga de datos y generación documentos
        {
            // mensaje de validación de archivo CSV
            ViewBag.Mensaje = String.Empty;
            ViewBag.Observaciones = false;
            List<string> listaObservacionesCSV = new List<string>();

            Plantilla_Dato pdTemp;
            List<Plantilla_Dato> listaPDGuardada = new List<Plantilla_Dato>();

            try
            {
                // recuperar objeto Plantilla
                plantilla = GetPlantillaById(plantilla.Id);

                // verificar que exista un archivo y sea en formato .CSV
                if (file != null && file.ContentLength > 0)
                {
                    // verificar que sea archivo .CSV
                    if (System.IO.Path.GetExtension(file.FileName).ToUpper() == ".CSV")
                    {
                        // guardar temporalmente archivo CSV
                        string nombreArchivoCSV = System.IO.Path.GetFileName(file.FileName).Trim().Replace(" ", "_");
                        string pathArchivoCSV = System.IO.Path.Combine(Server.MapPath("~/Temporal/"), nombreArchivoCSV);
                        file.SaveAs(pathArchivoCSV);

                        // lectura de datos del archivo CSV, revisar observaciones y crear lista de datos
                        List<Datos> listaDatosRegistrar = new List<Datos>();
                        string csvData = System.IO.File.ReadAllText(pathArchivoCSV, UTF8Encoding.UTF8);
                        string[] csvDataArray = csvData.Replace("\r", "").Split('\n');

                        // si última fila está vacia y se elimina del array
                        if (String.IsNullOrWhiteSpace(csvDataArray.Last()))
                        {
                            csvDataArray = csvDataArray.Take(csvDataArray.Count() - 1).ToArray();
                        }

                        for (int i = 1; i < csvDataArray.Length; i++)
                        {
                            // verificar que la fila no esté vacía
                            if (!String.IsNullOrWhiteSpace(csvDataArray[i]))
                            {
                                // convertir columnas de cada fila en un array
                                string[] csvDataFila = csvDataArray[i].Split(',');

                                // verifica que las columnas no estén vacías
                                List<string> listaObservacionesFila = VerificarRegistroArchivo(csvDataFila, plantilla);

                                // verifica si hay observaciones que impidan guardar el registro
                                if (listaObservacionesFila.Count() == 0)
                                {
                                    // cargar objeto DATOS en lista
                                    Datos datoTemp = new Datos()
                                    {
                                        nombres_apellidos = csvDataFila[0].Trim().ToUpper(),
                                        curso_taller = csvDataFila[1].Trim(),
                                        fecha = csvDataFila[2].Trim(),
                                        fecha_registro = DateTime.Now
                                    };
                                    datoTemp.opcional1 = plantilla.opcional1_plantilla ? csvDataFila[3].Trim() : "0";
                                    datoTemp.opcional2 = plantilla.opcional2_plantilla ? csvDataFila[4].Trim() : "0";
                                    datoTemp.opcional3 = plantilla.opcional3_plantilla ? csvDataFila[5].Trim() : "0";
                                    datoTemp.opcional4 = plantilla.opcional4_plantilla ? csvDataFila[6].Trim() : "0";

                                    listaDatosRegistrar.Add(datoTemp);
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

                        // se elimina archivo temporal
                        //System.IO.File.Delete(pathArchivoCSV);
                        BorrarArchivo(pathArchivoCSV);

                        // si no existen observaciones, generar los documentos y registrar en DB
                        if (listaObservacionesCSV.Count() == 0 && listaDatosRegistrar.Count() > 0)
                        {
                            // guardar en tabla DATOS y PLANTILLA_DATO
                            foreach (Datos datoRegistrar in listaDatosRegistrar)
                            {

                                // registrar en tabla DATOS
                                dbContextPlantillas.Datos.Add(datoRegistrar);

                                // DOCUMENTO: generar pdf y registrar objeto
                                // path archivo plantilla
                                string pathPlantilla = "~/PlantillasOtros/";
                                string nombreArchivoPlantilla = plantilla.archivo_plantilla;

                                // abrir plantilla
                                var pdfReader = new PdfReader(Request.MapPath(pathPlantilla + nombreArchivoPlantilla));

                                // buffer para almacenar
                                MemoryStream ms = new MemoryStream();
                                PdfWriter pw = new PdfWriter(ms);
                                PdfDocument pdf = new PdfDocument(pdfReader, pw);
                                Document doc = new Document(pdf, PageSize.A4);

                                // mapeo de datos - campos obligatorios
                                PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                                IDictionary<String, PdfFormField> fields = form.GetFormFields();

                                fields.TryGetValue("nombres_apellidos", out PdfFormField toSet);
                                toSet.SetValue(datoRegistrar.nombres_apellidos.ToString());
                                fields.TryGetValue("curso_taller", out toSet);
                                toSet.SetValue(datoRegistrar.curso_taller.ToString());
                                fields.TryGetValue("fecha", out toSet);
                                toSet.SetValue(datoRegistrar.fecha.ToString());

                                // campos opcionales
                                if (plantilla.opcional1_plantilla)
                                {
                                    fields.TryGetValue("opcional1", out toSet);
                                    toSet.SetValue(datoRegistrar.opcional1.ToString());
                                }
                                if (plantilla.opcional2_plantilla)
                                {
                                    fields.TryGetValue("opcional2", out toSet);
                                    toSet.SetValue(datoRegistrar.opcional2.ToString());
                                }
                                if (plantilla.opcional3_plantilla)
                                {
                                    fields.TryGetValue("opcional3", out toSet);
                                    toSet.SetValue(datoRegistrar.opcional3.ToString());
                                }
                                if (plantilla.opcional4_plantilla)
                                {
                                    fields.TryGetValue("opcional4", out toSet);
                                    toSet.SetValue(datoRegistrar.opcional4.ToString());
                                }
                                form.FlattenFields();

                                // generar y agregar código QR
                                ImageData imageData = ImageDataFactory.CreatePng(GenerarQRByte(datoRegistrar.nombres_apellidos));
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

                                // guardar archivo en servidor
                                string nombreDocumento = datoRegistrar.nombres_apellidos.Trim().Replace(" ", "_") + "_" + DateTime.Now.GetHashCode().ToString().Replace("-", "") + ".pdf";
                                string destinoDocumento = Server.MapPath("~/Documentos/OtrosDocumentos/");
                                FileStream fileStream = new FileStream(destinoDocumento + nombreDocumento, FileMode.Create, FileAccess.ReadWrite);
                                ms.WriteTo(fileStream);
                                fileStream.Close();

                                // registrar en tabla DOCUMENTOS
                                Documentos docTemp = new Documentos()
                                {
                                    dato_id = datoRegistrar.Id,
                                    fecha_generado = DateTime.Today,
                                    ruta_archivo = nombreDocumento,
                                    codigo_verificacion = CreateHashString(nombreDocumento)
                                };
                                dbContextPlantillas.Documentos.Add(docTemp);

                                // registrar en tabla PLATILLA_DATO
                                pdTemp = new Plantilla_Dato()
                                {
                                    plantilla_id = plantilla.Id,
                                    dato_id = datoRegistrar.Id
                                };
                                dbContextPlantillas.Plantilla_Dato.Add(pdTemp);

                                // guardar registros en DB
                                dbContextPlantillas.SaveChanges();
                                listaPDGuardada.Add(GetPDByDatoId(pdTemp.dato_id));
                            }

                            // respuesta a VIEW
                            ViewBag.CantidadDatos = listaPDGuardada.Count();
                            string datosIdString = String.Empty;
                            if (listaPDGuardada.Count() > 0)
                            {
                                foreach (Plantilla_Dato pd in listaPDGuardada)
                                {
                                    datosIdString += pd.dato_id.ToString() + ",";
                                }
                            }

                            ViewBag.NombreArchivo = GetDocumentoByDatoId(listaPDGuardada.ElementAt(0).dato_id).ruta_archivo;
                            ViewBag.DatosIdString = datosIdString;
                            return View(plantilla);
                        }
                        else if (listaObservacionesCSV.Count() == 0 && listaDatosRegistrar.Count() == 0)
                        {
                            ViewBag.Mensaje = "El archivo cargado está vacío.";
                            ViewData["listaObservaciones"] = listaObservacionesCSV;
                            return View("SeleccionarDatos", plantilla);
                        }
                        else
                        {
                            ViewBag.Observaciones = true;
                            ViewBag.Mensaje = "No se puede cargar el archivo \'" + file.FileName + " \' porque contiene las siguientes observaciones.";
                            ViewData["listaObservaciones"] = listaObservacionesCSV;
                            return View("SeleccionarDatos", plantilla);
                        }
                    }
                    else
                    {
                        ViewBag.Mensaje = "El archivo cargado no tiene extensión CSV.";
                        return View("SeleccionarDatos", plantilla);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "Para generar los documentos, debe subir un archivo en formato CSV.";
                    return View("SeleccionarDatos", plantilla);
                }
            }
            catch (Exception e)
            {
                ViewBag.Mensaje = "Se ha producido un error al momento de cargar los datos y generar el documento";
                ViewBag.MensajeError = "Error producido" + e.Message.ToString();
                return View("ReporteError");
            }
        }

        public ActionResult DescargarDocumento(string nombreArchivo)        // descarga individual de DOCUMENTO generado
        {
            string pathDescarga = Server.MapPath("~/Documentos/OtrosDocumentos/");
            byte[] fileBytes = System.IO.File.ReadAllBytes(pathDescarga + nombreArchivo);
            return File(fileBytes, "application/pdf", nombreArchivo);
        }

        public ActionResult DescargarZipDocumentos(string datosIdString)    // descarga ZIP de documentos generados
        {
            // obtener lista de objetos documentos generados
            string[] datosIdArray = datosIdString.Split(',');
            List<Documentos> listaDocumentos = new List<Documentos>();
            foreach (string datoIdString in datosIdArray)
            {
                if (!String.IsNullOrWhiteSpace(datoIdString))
                {
                    if (Int32.TryParse(datoIdString, out int datoId))
                    {
                        listaDocumentos.Add(GetDocumentoByDatoId(datoId));
                    }
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
        

        // métodos de soporte -----------------------------------------------------------

        public List<Plantillas> GetPlantillasActivas()
        {
            var result = from p in dbContextPlantillas.Plantillas
                         where p.plantilla_activa == true
                         orderby p.fecha_creacion descending
                         select p;
            return result.Any() ? result.ToList() : null;
        }

        public Plantillas GetPlantillaById(int plantilla_id)
        {
            var result = from p in dbContextPlantillas.Plantillas
                         where p.Id == plantilla_id
                         select p;
            return result.Any() ? result.First() : null;
        }

        public List<Datos> GetDatos()
        {
            var result = from d in dbContextPlantillas.Datos
                         select d;
            return result.Any() ? result.ToList() : null;
        }

        public bool VerificarDatoById(int id)
        {
            var result = from d in dbContextPlantillas.Plantilla_Dato
                         where d.dato_id == id
                         select d;
            return result.Any();
        }

        public Datos GetDatoById(int idDato)
        {
            var result = from d in dbContextPlantillas.Datos
                         where d.Id == idDato
                         select d;
            return result.Any() ? result.First() : null;
        }

        public List<Plantilla_Dato> GetPDByPlantillaId(int id)
        {
            var result = from pd in dbContextPlantillas.Plantilla_Dato
                         where pd.plantilla_id == id
                         select pd;
            return result.Any() ? result.ToList() : null;
        }

        public Plantilla_Dato GetPDByDatoId(int id)
        {
            var result = from pd in dbContextPlantillas.Plantilla_Dato
                         where pd.dato_id == id
                         select pd;
            return result.Any() ? result.First() : null;
        }

        public Plantilla_Dato GetPDById(int id)
        {
            var result = from pd in dbContextPlantillas.Plantilla_Dato
                         where pd.Id == id
                         select pd;
            return result.Any() ? result.First() : null;
        }

        public Plantillas GetPlantillaByDatoId(int idDato)
        {
            var result = from pd in dbContextPlantillas.Plantilla_Dato
                         where pd.dato_id == idDato
                         select pd.plantilla_id;
            return result.Any() ? GetPlantillaById(result.First()) : null;
        }

        public List<Plantilla_Dato> GetPDByDatosId(List<Datos> listaDatos)
        {
            List<Plantilla_Dato> listaPD = new List<Plantilla_Dato>();
            foreach (Datos dato in listaDatos)
            {
                var result = from pd in dbContextPlantillas.Plantilla_Dato
                             where pd.dato_id == dato.Id
                             select pd;
                if (result.Any())
                {
                    listaPD.Add(result.First());
                }
            }
            return listaPD;
        }

        public Documentos GetDocumentoByDatoId(int idDato)
        {
            var result = from doc in dbContextPlantillas.Documentos
                         where doc.dato_id == idDato
                         select doc;
            return result.Any() ? result.First() : null;
        }

        public bool BuscarNombrePlantilla(string nombrePlantilla)
        {
            var result = from p in dbContextPlantillas.Plantillas
                         where p.name == nombrePlantilla
                         select p;
            return result.Any();
        }

        public byte[] GenerarQRByte(string hashText)   // generar código QR con información del Comerciante
        {
            // información del comerciantes que irá en el código QR
            //string dataForQR = $"https://localhost:44320/Comerciantes/ValidarCertificado/{hashText}";
            string dataForQR = hashText;

            // generar código QR - información
            QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
            QRCodeData qRCodeData = qRCodeGenerator.CreateQrCode(dataForQR, QRCodeGenerator.ECCLevel.Q);

            // renderizar - representación
            PngByteQRCode qrCode = new PngByteQRCode(qRCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);
            //return Convert.ToBase64String(qrCodeImage);   para devolver como string
            return qrCodeImage;
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
                    return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
                }
            }
            else
            {
                return String.Empty;
            }
        }

        public void BorrarArchivo(string pathArchivo)
        {
            if (System.IO.File.Exists(pathArchivo))
            {
                System.IO.File.Delete(pathArchivo);
            }
        }

        public string GetSalt()
        {
            var resultado = (from p in dbContextPlantillas.ParametrosCC
                             where p.nombre_parametro == "SaltHash"
                             select p.valor_parametro).First();
            return resultado;
        }

        public bool TextoContieneEspacios(string texto)
        {
            return texto.Any(char.IsWhiteSpace);
        }

        public List<string> VerificarRegistroArchivo(string[] datosFilaCSV, Plantillas plantilla)
        {
            List<string> resultadoValidacion = new List<string>();

            // validación NOMBRES_APELLIDOS
            if (String.IsNullOrWhiteSpace(datosFilaCSV[0].Trim()))
            {
                resultadoValidacion.Add("El campo NOMBRES_APELLIDOS está vacío");
            }

            // validación CURSO_TALLER
            if (String.IsNullOrWhiteSpace(datosFilaCSV[1].Trim()))
            {
                resultadoValidacion.Add("El campo CURSO_TALLER está vacío");
            }

            // validación FECHA
            if (String.IsNullOrWhiteSpace(datosFilaCSV[2].Trim()))
            {
                resultadoValidacion.Add("El campo FECHA está vacío");
            }

            // validación opcional1
            if (plantilla.opcional1_plantilla)
            {
                if (String.IsNullOrWhiteSpace(datosFilaCSV[3].Trim()))
                {
                    resultadoValidacion.Add("El campo OPCIONAL1 está vacío");
                }
            }

            // validación opcional2
            if (plantilla.opcional2_plantilla)
            {
                if (String.IsNullOrWhiteSpace(datosFilaCSV[4].Trim()))
                {
                    resultadoValidacion.Add("El campo OPCIONAL2 está vacío");
                }
            }

            // validación opcional3
            if (plantilla.opcional3_plantilla)
            {
                if (String.IsNullOrWhiteSpace(datosFilaCSV[5].Trim()))
                {
                    resultadoValidacion.Add("El campo OPCIONAL3 está vacío");
                }
            }

            // validación opcional4
            if (plantilla.opcional4_plantilla)
            {
                if (String.IsNullOrWhiteSpace(datosFilaCSV[6].Trim()))
                {
                    resultadoValidacion.Add("El campo OPCIONAL4 está vacío");
                }
            }

            return resultadoValidacion;
        }
    }
}