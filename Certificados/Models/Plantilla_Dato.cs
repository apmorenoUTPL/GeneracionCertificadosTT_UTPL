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
    
    public partial class Plantilla_Dato
    {
        public int Id { get; set; }
        public int plantilla_id { get; set; }
        public int dato_id { get; set; }
    
        public virtual Datos Datos { get; set; }
        public virtual Plantillas Plantillas { get; set; }
    }
}
