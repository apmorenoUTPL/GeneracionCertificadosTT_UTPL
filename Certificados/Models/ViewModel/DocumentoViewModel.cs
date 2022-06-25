using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Certificados.Models.ViewModel
{
    public class DocumentoViewModel
    {
        public string Nombres_Apellidos { get; set; }
        public string Curso_Taller { get; set; }
        public string Fecha_Curso_Taller { get; set; }
        public string Fecha { get; set; }
        public string Archivo { get; set; }
        public string Codigo_Verificacion { get; set; }
    }
}