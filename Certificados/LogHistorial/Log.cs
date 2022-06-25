using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Certificados.Models;

namespace Certificados.LogHistorial
{
    [Authorize]
    public class Log
    {
        private readonly ComerciantesEntities dbContextLog = new ComerciantesEntities();
                
        public ComerciantesEntities DbContextLog => dbContextLog;

        private readonly string DirectorioPath = String.Empty;

        private static object locker = new Object();

        public Log() 
        {
            DirectorioPath = "~/LogRegistros/" + DateTime.Today.Year + "/" + DateTime.Today.Month + "/";
        }

        public void Add(Log_Registros logRegistro)
        {
            try
            {
                // guardar en DB
                logRegistro.fecha_log = DateTime.Now;
                if (String.IsNullOrWhiteSpace(logRegistro.excepcion_log))
                {
                    logRegistro.excepcion_log = "No genera Excepción";
                }
                dbContextLog.Log_Registros.Add(logRegistro);
                if (dbContextLog.SaveChanges() > 0)
                {
                    // guardar en archivo log
                    CrearDirectorio();
                    string textoInsertar = logRegistro.fecha_log.ToString() + " -- " + logRegistro.tipo_log + " -- " 
                        + logRegistro.usuario_log + " -- " + logRegistro.mensaje_log + " -- " + logRegistro.excepcion_log + Environment.NewLine;
                    string _path = Path.Combine(HttpContext.Current.Server.MapPath(DirectorioPath), GetNombreArchivo());

                    lock (locker)
                    {
                        using (FileStream fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                        {
                            sw.Write(textoInsertar);
                            sw.Close();
                        }
                    }
                    
                    //StreamWriter sw = new StreamWriter(_path, true, Encoding.UTF8);
                    //
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public void Error(Log_Registros logRegistro)
        {
            logRegistro.tipo_log = "ERROR";
            Add(logRegistro);
        }

        public void Warning(Log_Registros logRegistro)
        {
            logRegistro.tipo_log = "WARNING";
            Add(logRegistro);
        }

        public void Transaction(Log_Registros logRegistro)
        {
            logRegistro.tipo_log = "TRANSACTION";
            Add(logRegistro);
        }

        public void Info(Log_Registros logRegistro)
        {
            logRegistro.tipo_log = "INFO";
            Add(logRegistro);
        }

        #region METODOS
        private string GetNombreArchivo()
        {
            return "log_" + DateTime.Today.ToString("dd-MM-yyyy") + ".txt";
        }

        private void CrearDirectorio()
        {
            try
            {
                if (!Directory.Exists(HttpContext.Current.Server.MapPath(DirectorioPath)))
                {
                    Directory.CreateDirectory(HttpContext.Current.Server.MapPath(DirectorioPath));
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion
    }
}