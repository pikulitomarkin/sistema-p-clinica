using System.ComponentModel;
using System.Reflection;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            if (fieldInfo == null) return enumValue.ToString();

            var descriptionAttributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (descriptionAttributes != null && descriptionAttributes.Length > 0)
            {
                return descriptionAttributes[0].Description;
            }

            // Fallback para nomes específicos
            return enumValue switch
            {
                StatusConsulta.Agendada => "Agendada",
                StatusConsulta.Confirmada => "Confirmada", 
                StatusConsulta.Realizada => "Realizada",
                StatusConsulta.Cancelada => "Cancelada",
                StatusConsulta.NoShow => "No-Show",
                StatusConsulta.Reagendada => "Reagendada",
                TipoConsulta.Normal => "Normal",
                TipoConsulta.Gratuita => "Gratuita",
                TipoConsulta.Retorno => "Retorno",
                TipoConsulta.Avaliacao => "Avaliação",
                TipoNotificacao.Email => "Email",
                TipoNotificacao.WhatsApp => "WhatsApp",
                TipoNotificacao.SMS => "SMS",
                TipoNotificacao.Push => "Push",
                TipoUsuario.Admin => "Administrador",
                TipoUsuario.Psicologo => "Psicólogo",
                TipoUsuario.Cliente => "Cliente",
                _ => enumValue.ToString()
            };
        }
    }
}