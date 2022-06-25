using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Certificados.Models.ViewModel
{
    public class ComercianteEstadoRectViewModel
    {
        // se utiliza para completar el datatable de RECTIFICACIONES en el TecnicoController        
        public int Id { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Cedula { get; set; }
        public string Capacitacion { get; set; }
        public string Institucion { get; set; }
        public string RectificacionGenerada { get; set; }
    }
}