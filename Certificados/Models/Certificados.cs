//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Certificados.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Certificados
    {
        public int Id { get; set; }
        public System.DateTime fecha_emision { get; set; }
        public int num_certificado { get; set; }
        public int comerciantes_id { get; set; }
        public string codigo_verificacion { get; set; }
        public bool certificado_valido { get; set; }
        public string ruta_archivo { get; set; }
        public int plantillascc_id { get; set; }
    
        public virtual Comerciantes Comerciantes { get; set; }
        public virtual PlantillasCC PlantillasCC { get; set; }
    }
}
