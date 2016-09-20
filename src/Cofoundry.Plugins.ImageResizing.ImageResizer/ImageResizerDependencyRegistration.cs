using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cofoundry.Core.DependencyInjection;
using Cofoundry.Domain;

namespace Cofoundry.Plugins.ImageResizing.ImageResizer
{
    public class ImageResizerDependencyRegistration : IDependencyRegistration
    {
        public void Register(IContainerRegister container)
        {
            var overrideOptions = new RegistrationOptions()
            {
                ReplaceExisting = true,
                RegistrationOverridePriority = (int)RegistrationOverridePriority.Low
            };

            container
                .RegisterType<IResizedImageAssetFileService, ImageResizerResizedImageAssetFileService>(overrideOptions)
                ;
        }
    }
}
