﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SimpleMvc.Framework.Attributes.Methods;
using SimpleMvc.Framework.Contracts;
using SimpleMvc.Framework.Controllers;
using WebServer.Contracts;
using WebServer.Enums;
using WebServer.Http.Contracts;
using WebServer.Http.Response;

namespace SimpleMvc.Framework.Routers
{
    public class ControllerRouter : IHandleable
    {
        private IDictionary<string, string> getParams;
        private IDictionary<string, string> postParams;
        private string requestMethod;
        private string controllerName;
        private string actionName;
        private object[] methodParams;

        public ControllerRouter()
        {
            this.getParams = new Dictionary<string, string>();
            this.postParams = new Dictionary<string, string>();
        }

        public IHttpResponse Handle(IHttpRequest request)
        {
            //1. Parse input from request
            //-retrieve Get parameters

            this.getParams = request.UrlParameters;

            //-retrieve Post parameters
            this.postParams = request.FormData;

            //retrieve request method
            this.requestMethod = request.Method.ToString();

            //retrieve action name
            var urlParams = request.Path
              .Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);


            if (urlParams.Length != 2)
            {
                throw new ArgumentException("Invalid URL!");

            }

            this.controllerName = String
                .Concat(
                Char.ToUpper(urlParams[0][0]),
                urlParams[0]
                    .Substring(1, urlParams[0].Length - 1),
                MvcContext.Get.ControllersSuffix);

            this.actionName = String
                .Concat(
                    Char.ToUpper(urlParams[1][0]),
                urlParams[1].Substring(1, urlParams[1].Length - 1));

            MethodInfo method = this.GetMethod();

            if (method == null)
            {
                return new NotFoundResponse();
            }

            IEnumerable<ParameterInfo> parameters = method.GetParameters();
            this.methodParams = new object[parameters.Count()];

            int index = 0;

            foreach (var param in parameters)
            {
                if (param.ParameterType.IsPrimitive || param.ParameterType == typeof(string))
                {
                    object value = this.getParams[param.Name];
                    this.methodParams[index] = Convert.ChangeType(value, param.ParameterType);
                    index++;
                }
                else
                {
                    Type bindingModelType = param.ParameterType;
                    object bindingModel = Activator.CreateInstance(bindingModelType);

                    IEnumerable<PropertyInfo> properties = bindingModelType.GetProperties();

                    foreach (var property in properties)
                    {
                        property.SetValue(
                            bindingModel,
                            Convert.ChangeType(postParams[property.Name],
                            property.PropertyType));
                    }

                    this.methodParams[index] = Convert.ChangeType(
                        bindingModel,
                        bindingModelType);

                    index++;
                }
            }

            method = this.GetMethod();

            IInvocable actionResult = (IInvocable)method
                    .Invoke(this.GetController(), this.methodParams);

            string content = actionResult.Invoke();

            IHttpResponse response = new ContentResponse(HttpStatusCode.Ok, content);

            return response;


        }

        private MethodInfo GetMethod()
        {
            MethodInfo method = null;

            foreach (MethodInfo methodInfo in this.GetSuitableMethods())
            {
                IEnumerable<Attribute> attributes = methodInfo
                    .GetCustomAttributes()
                    .Where(a => a is HttpMethodAttribute);
                if (!attributes.Any() && this.requestMethod == "GET")
                {
                    return methodInfo;
                }

                foreach (HttpMethodAttribute attribute in attributes)
                {
                    if (attribute.IsValid(this.requestMethod))
                    {
                        return methodInfo;
                    }
                }
            }

            return method;
        }

        private IEnumerable<MethodInfo> GetSuitableMethods()
        {
            var controller = this.GetController();
            if (controller == null)
            {
                return new MethodInfo[0];
            }

            return this.GetController()
                .GetType()
                .GetMethods()
                .Where(p => p.Name == actionName);


        }

        private object GetController()
        {
            var controllerFullQualifiedName = string.Format(
                "{0}.{1}.{2}, {0}",
                MvcContext.Get.AssemblyName,
                MvcContext.Get.ControllersFolder,
                this.controllerName);

            Type type = Type.GetType(controllerFullQualifiedName);

            if (type == null)
            {
                return null;
            }

            var controller = (Controller)Activator.CreateInstance(type);
            return controller;

        }
    }
}



