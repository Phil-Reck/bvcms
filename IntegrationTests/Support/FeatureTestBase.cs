﻿using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SharedTestFixtures;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using Xunit;

namespace IntegrationTests.Support
{
    public abstract class FeatureTestBase : DatabaseTestBase
    {
        /// <summary>
        /// http://localhost:80/ including the slash
        /// </summary>
        protected string rootUrl => Settings.RootUrl;

        protected const string loadingUI = "div.blockUI.blockOverlay";

        protected IWebDriver driver;

        protected StringBuilder verificationErrors;

        public static FeatureTestBase Current { get; private set; }

        protected abstract bool UseSharedDriver { get; }

        protected IJavaScriptExecutor script
        {
            [System.Diagnostics.DebuggerStepThrough]
            get { return (IJavaScriptExecutor)driver; }
        }

        protected ITakesScreenshot screenShotDriver
        {
            [System.Diagnostics.DebuggerStepThrough]
            get { return (ITakesScreenshot)driver; }
        }

        public FeatureTestBase() : base()
        {
            Current = this;
            verificationErrors = new StringBuilder();
            StartBrowser();
        }

        protected void StartBrowser()
        {
            if (driver != null && !UseSharedDriver)
            {
                driver.Quit();
                driver = null;
            }

            driver = WebAppFixture.GetWebDriver(UseSharedDriver);
            if (UseSharedDriver)
            {
                ClearCookies();
            }
        }

        private bool _disposed;
        public override void Dispose()
        {
            if (!_disposed)
            {
                Current = null;
                _disposed = true;

                try
                {
                    if (!UseSharedDriver)
                    {
                        driver?.Quit();
                        driver = null;
                    }
                }
                catch (Exception)
                {
                    // Ignore errors if unable to close the browser
                }

                base.Dispose();
            }
        }

        /// <summary>
        /// Pauses execution for the given number of milliseconds
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <seealso cref="Wait(double)"/>
        [System.Diagnostics.DebuggerStepThrough]
        protected void Sleep(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }

        internal void ClearCookies()
        {
            driver.Manage().Cookies.DeleteAllCookies();
        }

        /// <summary>
        /// Finds the IWebElement by the given attributes
        /// </summary>
        /// <param name="by">An instance of a By class</param>
        /// <param name="css">A CSS expression that evaluates to an element</param>
        /// <param name="id">An element ID</param>
        /// <param name="match">Partial link text to match</param>
        /// <param name="name">Value of the name attribute on the element to find</param>
        /// <param name="tag">Tag name of the element to find</param>
        /// <param name="text">Exact link text to match</param>
        /// <param name="xpath">Xpath expression to select from the document</param>
        /// <returns>an IWebElement instance or null</returns>
        protected IWebElement Find(By by = null,
            string css = null,
            string id = null,
            string match = null,
            string name = null,
            string tag = null,
            string text = null,
            string xpath = null,
            bool visible = true)
        {
            try
            {
                if (css != null)
                {
                    by = By.CssSelector(css);
                }
                if (text != null)
                {
                    by = By.LinkText(text);
                }
                if (match != null)
                {
                    by = By.PartialLinkText(match);
                }
                if (id != null)
                {
                    by = By.Id(id);
                }
                if (name != null)
                {
                    by = By.Name(name);
                }
                if (tag != null)
                {
                    by = By.TagName(tag);
                }
                if (xpath != null)
                {
                    by = By.XPath(xpath);
                }
                if (by != null)
                {
                    if (visible)
                    {
                        return NonNullElement(by, driver.FindElements(by).First(e => e.Displayed));
                    }
                    else
                    {
                        return NonNullElement(by, driver.FindElement(by));
                    }
                }
            }
            catch
            {
            }
            return NonNullElement(by, null);
        }

        private IWebElement NonNullElement(By by, IWebElement webElement)
        {
            if (webElement == null)
            {
                Console.WriteLine("Element not found: {0}", by.ToString());
            }
            return webElement;
        }

        protected void RepeatUntil(Action action, Func<bool> condition, int maxIterations = 10)
        {
            int iterations = 0;
            do
            {
                action();
                iterations++;
            } while (maxIterations > iterations && !condition());
        }
        
        protected void ScrollTo(IWebElement by = null,
            string css = null,
            string id = null,
            string match = null,
            string name = null,
            string tag = null,
            string text = null,
            string xpath = null,
            bool visible = true)
        {
            try
            {
                if (css != null)
                {
                    by = Find(css: css);
                }
                if (text != null)
                {
                    by = Find(text: text);
                }
                if (match != null)
                {
                    by = Find(match: match);
                }
                if (id != null)
                {
                    by = Find(id: id);
                }
                if (name != null)
                {
                    by = Find(name: name);
                }
                if (tag != null)
                {
                    by = Find(tag: tag);
                }
                if (xpath != null)
                {
                    by = Find(xpath: xpath);
                }
                if (by != null)
                {
                    Actions actions = new Actions(driver);
                    actions.MoveToElement(by);
                    actions.Perform();
                }
            }
            catch
            {
            }
        }

        internal void SaveScreenshot(string name = "Screenshot")
        {
            Screenshot screenshot = screenShotDriver.GetScreenshot();
            string file = $"{name}_" + DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss_") + RandomString() + ".png";
            string filename = Path.Combine(Settings.ScreenShotLocation, file);
            screenshot.SaveAsFile(filename, ScreenshotImageFormat.Png);
            Console.WriteLine("Screen shot saved: {0}", Path.Combine(Settings.ScreenShotUrl, file));
        }

        internal void ShouldNotHaveScriptError()
        {
            IWebElement JSErrors = null;
            try
            {
                JSErrors = driver.FindElement(By.Id("JSErrors"));
            }
            catch { }
            if (JSErrors != null)
            {
                verificationErrors.Append(JSErrors.Text);
            }

            verificationErrors.ToString().ShouldBeEmpty();
        }

        protected IEnumerable<IWebElement> FindAll(By by = null,
            string css = null,
            string id = null,
            string match = null,
            string name = null,
            string tag = null,
            string text = null,
            string xpath = null,
            bool visible = true)
        {
            try
            {
                if (css != null)
                {
                    by = By.CssSelector(css);
                }
                if (text != null)
                {
                    by = By.LinkText(text);
                }
                if (match != null)
                {
                    by = By.PartialLinkText(match);
                }
                if (id != null)
                {
                    by = By.Id(id);
                }
                if (name != null)
                {
                    by = By.Name(name);
                }
                if (tag != null)
                {
                    by = By.TagName(tag);
                }
                if (xpath != null)
                {
                    by = By.XPath(xpath);
                }
                if (by != null)
                {
                    if (visible)
                    {
                        return driver.FindElements(by).Where(e => e.Displayed);
                    }
                    else
                    {
                        return driver.FindElements(by);
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        protected IWebElement FindText(string text)
        {
            return driver.FindElements(By.XPath("//*[contains(text(),'" + text + "')]")).FirstOrDefault();
        }

        /// <summary>
        /// Finds a tab or window given the criteria specified in the <paramref name="predicate"/>
        /// </summary>
        /// <param name="predicate">Selector for the tab or window to switch to</param>
        protected void SwitchToWindow(Expression<Func<IWebDriver, bool>> predicate)
        {
            var exp = predicate.Compile();
            foreach (var handle in driver.WindowHandles)
            {
                driver.SwitchTo().Window(handle);
                if (exp(driver))
                {
                    return;
                }
            }

            throw new ArgumentException(string.Format("Unable to find window with condition: '{0}'", predicate.Body));
        }

        /// <summary>
        /// Navigates the browser to the given <paramref name="url"/>
        /// </summary>
        /// <param name="url">The URL to open; may or may not start with http://</param>
        protected void Open(string url, bool redirects = false)
        {
            string location = url.StartsWith("http") ? url : "http://" + url;
            Console.WriteLine("Opening: {0}", location);
            driver.Navigate().GoToUrl(location);
            if (!redirects)
            {
                WaitFor((d) => d.Url.Contains(url));
            }
        }

        /// <summary>
        /// Returns the Page HTML source
        /// </summary>
        protected string PageSource
        {
            [System.Diagnostics.DebuggerStepThrough]
            get { return driver.PageSource; }
        }

        /// <summary>
        /// Returns the current url loaded in the browser
        /// </summary>
        protected string CurrentUrl
        {
            [System.Diagnostics.DebuggerStepThrough]
            get { return driver.Url; }
        }

        /// <summary>
        /// Pauses execution for the given number of seconds
        /// </summary>
        /// <param name="seconds"></param>
        [System.Diagnostics.DebuggerStepThrough]
        protected double Wait(double seconds)
        {
            Thread.Sleep(Convert.ToInt32(seconds * 1000));
            return seconds;
        }

        protected bool IsElementPresent(By by)
        {
            try
            {
                driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }
        protected bool IsElementPresent(string css)
        {
            try
            {
                driver.FindElement(By.CssSelector(css));
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        protected bool IsAlertPresent()
        {
            try
            {
                driver.SwitchTo().Alert();
                return true;
            }
            catch (NoAlertPresentException)
            {
                return false;
            }
        }

        internal void MaximizeWindow(IWindow window = null)
        {
            (window ?? driver.Manage().Window).Maximize();
        }

        protected void WaitFor(Func<IWebDriver, bool> condition, int maxWaitTimeInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitTimeInSeconds));
            try
            {
                wait.Until(condition);
            }
            catch
            {
                SaveScreenshot();
                throw;
            }
        }

        protected void WaitForUrl(string url, int maxWaitTimeInSeconds = 10)
        {
            WaitFor(d =>
            {
                return driver.Url.Contains(url);
            }, maxWaitTimeInSeconds);
        }

        protected void WaitForElementToDisappear(string css, int maxWaitTimeInSeconds = 10, bool visible = true)
        {
            IWebElement element = null;
            try
            {
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitTimeInSeconds));

                wait.Until(d =>
                {
                    try
                    {
                        element = Find(By.CssSelector(css), visible: visible);
                    }
                    catch (InvalidOperationException)
                    {
                        //Ignore
                    }
                    catch (NoSuchWindowException)
                    {
                        //when popup is closed, switch to last windows
                        driver.SwitchTo().Window(driver.WindowHandles.Last());
                    }
                    //In IE7 there are chances we may get state as loaded instead of complete
                    return (element == null);
                });
            }
            catch (TimeoutException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (element == null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (NullReferenceException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (element != null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (WebDriverException)
            {
                if (driver.WindowHandles.Count == 1)
                {
                    driver.SwitchTo().Window(driver.WindowHandles.First());
                }
                element = driver.FindElement(By.CssSelector(css));
                if (element != null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
        }

        protected void WaitForElement(string css, int maxWaitTimeInSeconds = 10, bool visible = true)
        {
            IWebElement element = null;
            try
            {
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitTimeInSeconds));

                wait.Until(d =>
                {
                    try
                    {
                        element = Find(By.CssSelector(css), visible: visible);
                    }
                    catch (InvalidOperationException)
                    {
                        //Ignore
                    }
                    catch (NoSuchWindowException)
                    {
                        //when popup is closed, switch to last windows
                        driver.SwitchTo().Window(driver.WindowHandles.Last());
                    }
                    //In IE7 there are chances we may get state as loaded instead of complete
                    return (element != null);
                });
            }
            catch (TimeoutException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (element == null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (NullReferenceException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (element == null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (WebDriverException)
            {
                if (driver.WindowHandles.Count == 1)
                {
                    driver.SwitchTo().Window(driver.WindowHandles.First());
                }
                if (element == null)
                {
                    SaveScreenshot();
                    throw;
                }
            }
        }

        protected void WaitForPageLoad(int maxWaitTimeInSeconds = 10)
        {
            string state = string.Empty;
            try
            {
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitTimeInSeconds));

                //Checks every 500 ms whether predicate returns true; if returns exit otherwise keep trying till it returns true
                wait.Until(d =>
                {
                    try
                    {
                        state = ExecuteScript(@"return document.readyState").ToString();
                    }
                    catch (InvalidOperationException)
                    {
                        //Ignore
                    }
                    catch (NoSuchWindowException)
                    {
                        //when popup is closed, switch to last window
                        driver.SwitchTo().Window(driver.WindowHandles.Last());
                    }
                    //In IE7 there are chances we may get state as loaded instead of complete
                    return (state.Equals("complete", StringComparison.InvariantCultureIgnoreCase) || state.Equals("loaded", StringComparison.InvariantCultureIgnoreCase));
                });
            }
            catch (TimeoutException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (!state.Equals("interactive", StringComparison.InvariantCultureIgnoreCase))
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (NullReferenceException)
            {
                //sometimes Page remains in Interactive mode and never becomes Complete, then we can still try to access the controls
                if (!state.Equals("interactive", StringComparison.InvariantCultureIgnoreCase))
                {
                    SaveScreenshot();
                    throw;
                }
            }
            catch (WebDriverException)
            {
                if (driver.WindowHandles.Count == 1)
                {
                    driver.SwitchTo().Window(driver.WindowHandles.First());
                }
                state = ((IJavaScriptExecutor)driver).ExecuteScript(@"return document.readyState").ToString();
                if (!(state.Equals("complete", StringComparison.InvariantCultureIgnoreCase) || state.Equals("loaded", StringComparison.InvariantCultureIgnoreCase)))
                {
                    SaveScreenshot();
                    throw;
                }
            }
        }

        protected object ExecuteScript(string scriptToExecute)
        {
            return script.ExecuteScript(scriptToExecute);
        }
    }
}
