﻿// Copyright 2011 Max Toro Q.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Routing;
using System.Web.Mvc;
using System.Web;

namespace MvcCodeRouting {
   
   class CodeRoute : Route {

      internal static CodeRoute Create(IEnumerable<ActionInfo> actions) {

         if (actions == null) throw new ArgumentNullException("actions");

         ActionInfo first = actions.First();
         int count = actions.Count();
         var controllerNames = actions.Select(a => a.Controller.Name).Distinct().ToList();
         var actionNames = actions.Select(a => a.Name).Distinct().ToList();

         List<string> segments = new List<string>();
         segments.Add(first.Controller.UrlTemplate);

         if (controllerNames.Count == 1)
            segments[0] = first.Controller.ControllerUrl;

         segments.Add("{action}");

         bool hardcodeAction = actionNames.Count == 1
            && !(count == 1 && first.IsDefaultAction);

         if (hardcodeAction)
            segments[1] = first.Name;

         segments.AddRange(first.RouteParameters.Select(r => r.RouteSegment));

         string url = String.Join("/", segments.Where(s => !String.IsNullOrEmpty(s)));

         var defaults = new RouteValueDictionary();

         if (controllerNames.Count == 1)
            defaults.Add("controller", controllerNames.First());

         string defaultAction = null;

         if (actionNames.Count == 1) {
            defaultAction = first.Name;
         } else {
            var defAction = actions.FirstOrDefault(a => a.IsDefaultAction);

            if (defAction != null)
               defaultAction = defAction.Name;
         }

         if (defaultAction != null)
            defaults.Add("action", defaultAction);

         var parameters = first.RouteParameters;

         foreach (var param in parameters.Where(p => p.IsOptional))
            defaults.Add(param.Name, UrlParameter.Optional);

         var constraints = new RouteValueDictionary();

         if (controllerNames.Count > 1)
            constraints.Add("controller", String.Join("|", controllerNames));

         if (!hardcodeAction || actionNames.Count > 1)
            constraints.Add("action", String.Join("|", actionNames));

         foreach (var param in parameters.Where(p => p.Constraint != null)) {

            string regex = param.Constraint;

            if (param.IsOptional)
               regex = String.Concat("(", regex, ")?");

            constraints.Add(param.Name, regex);
         }

         constraints.Add(BaseRouteConstraint.Key, new BaseRouteConstraint());

         var dataTokens = new RouteValueDictionary { 
            { "Namespaces", new string[1] { first.Controller.Type.Namespace } },
            { "BaseRoute", String.Join("/", first.Controller.BaseRouteSegments) }
         };

         return new CodeRoute(url) { 
            Constraints = constraints,
            DataTokens = dataTokens,
            Defaults = defaults
         };
      }

      private CodeRoute(string url)
         : base(url, new MvcRouteHandler()) { }

      public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values) {

         string controller;
         string currentBaseRoute;
         RouteData routeData = requestContext.RouteData;

         if (values != null
            && (controller = values["controller"] as string) != null
            && (currentBaseRoute = routeData.DataTokens["BaseRoute"] as string) != null) {

            string baseRoute = currentBaseRoute;
            bool hasBaseRouteValue = values.ContainsKey(BaseRouteConstraint.Key);
            StringBuilder theController = new StringBuilder(controller);

            if (theController.Length > 0 && !hasBaseRouteValue) {

               List<string> baseRouteSegments = (baseRoute.Length > 0) ?
                  baseRoute.Split('/').ToList()
                  : new List<string>();

               if (theController[0] == '~') {

                  baseRouteSegments.Clear();
                  theController.Remove(0, 1);

               } else if (theController[0] == '.') {

                  if (theController.Length > 1 && theController[1] == '.') {

                     if (baseRouteSegments.Count == 0)
                        return null;

                     baseRouteSegments.RemoveAt(baseRouteSegments.Count - 1);
                     theController.Remove(0, 2);
                  
                  } else {

                     string[] namespaces = routeData.DataTokens["Namespaces"] as string[];

                     if (namespaces == null || namespaces.Length > 1)
                        return null;

                     baseRouteSegments.Add(namespaces[0].Split('.').Last());
                     theController.Remove(0, 1);
                  }
               }

               if (theController.Length > 1) {

                  string[] controllerSegments = theController.ToString().Split('.');

                  if (controllerSegments.Length > 1) {

                     baseRouteSegments.AddRange(controllerSegments.Take(controllerSegments.Length - 1));
                     
                     theController.Clear();
                     theController.Append(controllerSegments.Last());
                  }
               }

               baseRoute = String.Join(".", baseRouteSegments);
            }

            if (theController.Length == 0)
               values.Remove("controller");
            else
               values["controller"] = theController.ToString();

            if (!hasBaseRouteValue)
               values[BaseRouteConstraint.Key] = baseRoute;

            VirtualPathData virtualPath = base.GetVirtualPath(requestContext, values);

            values["controller"] = controller;

            if (!hasBaseRouteValue)
               values.Remove(BaseRouteConstraint.Key);

            return virtualPath;
         }

         return base.GetVirtualPath(requestContext, values);
      }
   }

   public class BaseRouteConstraint : IRouteConstraint {

      public const string Key = "__baseroute";

      public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection) {

         string controller;
         string baseRoute;
         string routeBaseRoute;

         if (routeDirection == RouteDirection.UrlGeneration
            && (controller = values["controller"] as string) != null
            && (baseRoute = values[Key] as string) != null
            && (routeBaseRoute = route.DataTokens["BaseRoute"] as string) != null) {

            return String.Equals(baseRoute, routeBaseRoute, StringComparison.Ordinal);
         }

         return true;
      }
   }
}
