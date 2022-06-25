using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Certificados.Models.ViewModel
{
    // se utiliza para completar el datatable de CERTIFICADOS en el TecnicoController
    public class ComercianteEstadoCCViewModel
    {
        public int Id { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Cedula { get; set; }
        public int Capacitacion { get; set; }
        public string Institucion { get; set; }
        public string CertificadoGenerado { get; set; }
    }
}