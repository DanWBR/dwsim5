'    DWSIM Nested Loops Flash Algorithms for Solid-Liquid Equilibria (SLE)
'    Copyright 2013 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports System.Math

Imports DWSIM.MathOps.MathEx
Imports DWSIM.MathOps.MathEx.Common

Imports System.Threading.Tasks

Imports System.Linq

Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    ''' <summary>
    ''' The Flash algorithms in this class are based on the Nested Loops approach to solve equilibrium calculations.
    ''' </summary>
    ''' <remarks></remarks>
    <System.Serializable()> Public Class NestedLoopsSLE

        Inherits FlashAlgorithm

        Dim etol As Double = 0.000001
        Dim itol As Double = 0.000001
        Dim maxit_i As Integer = 100
        Dim maxit_e As Integer = 100
        Dim Hv0, Hvid, Hlid, Hf, Hv, Hl, Hs As Double
        Dim Sv0, Svid, Slid, Sf, Sv, Sl, Ss As Double

        Public Property CompoundProperties As List(Of Interfaces.ICompoundConstantProperties)

        Public Property SolidSolution As Boolean = False

        Sub New()
            MyBase.New()
            Order = 3
        End Sub

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                If SolidSolution Then
                    Return Interfaces.Enums.FlashMethod.Nested_Loops_SLE_SolidSolution
                Else
                    Return Interfaces.Enums.FlashMethod.Nested_Loops_SLE_Eutectic
                End If
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                If GlobalSettings.Settings.CurrentCulture = "pt-BR" Then
                    If SolidSolution Then
                        Return "Algoritmo Flash para sistemas S�lido-L�quido (ESL)"
                    Else
                        Return "Algoritmo Flash para sistemas S�lido-L�quido-Vapor (ESLV)"
                    End If
                Else
                    If SolidSolution Then
                        Return "Flash Algorithm for Solid-Liquid (SLE) systems"
                    Else
                        Return "Flash Algorithm for Vapor-Solid-Liquid (VSLE) systems"
                    End If
                End If
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                If SolidSolution Then
                    Return "Nested Loops (SLE - Solid Solution)"
                Else
                    Return "Nested Loops (SVLE - Eutectic)"
                End If
            End Get
        End Property

        Public Overrides Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            CompoundProperties = PP.DW_GetConstantProperties

            If SolidSolution Then
                Return Flash_PT_SS(Vz, P, T, PP, ReuseKI, PrevKi)
            Else
                'Return Flash_PT_E(Vz, P, T, PP, ReuseKI, PrevKi)
                Return Flash_PT_NL(Vz, P, T, PP, ReuseKI, PrevKi)
            End If

        End Function

        Public Function Flash_PT_SS(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object


            Dim i, n, ecount As Integer
            Dim soma_x, soma_y, soma_s As Double
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, S, Lant, V As Double

            Dim ids As New List(Of String)

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            Dim Vn(n) As String, Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), Ki_ant(n), fi(n), Vs(n), Vs_ant(n), activcoeff(n) As Double

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            'Calculate Ki`s

            i = 0
            Do
                ids.Add(CompoundProperties(i).Name)
                Vp(i) = PP.AUX_PVAPi(i, T)
                If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                    Ki(i) = Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                    If Ki(i) = 0.0# Then Ki(i) = 1.0E+20
                Else
                    Ki(i) = 1.0E+20
                End If
                i += 1
            Loop Until i = n + 1

            V = 0.0#
            L = 1.0#
            S = 0.0#

            i = 0
            Do
                If Vz(i) <> 0.0# Then
                    Vx(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * L + 1)
                    If Double.IsNaN(Vx(i)) Then Vx(i) = 0.0#
                    If Ki(i) <> 0 Then Vs(i) = Vx(i) / Ki(i) Else Vs(i) = Vz(i)
                    If Vs(i) < 0 Then Vs(i) = 0
                    If Vx(i) < 0 Then Vx(i) = 0
                Else
                    Vs(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            soma_x = 0
            soma_s = 0
            soma_y = 0.0#
            Do
                soma_x = soma_x + Vx(i)
                soma_s = soma_s + Vs(i)
                i = i + 1
            Loop Until i = n + 1
            i = 0
            Do
                If soma_x > 0.0# Then Vx(i) = Vx(i) / soma_x
                If soma_s > 0.0# Then Vs(i) = Vs(i) / soma_s
                i = i + 1
            Loop Until i = n + 1

            ecount = 0
            Dim convergiu = 0
            Dim F = 0.0#


            Do



                Ki_ant = Ki.Clone

                activcoeff = PP.DW_CalcFugCoeff(Vx, T, P, State.Liquid)

                For i = 0 To n
                    If Double.IsNaN(activcoeff(i)) Then activcoeff(i) = 1.0#
                Next

                For i = 0 To n
                    If Not CompoundProperties(i).IsSalt Then activcoeff(i) = activcoeff(i) * P / Vp(i)
                    If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                        Ki(i) = (1 / activcoeff(i)) * Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                        If Ki(i) = 0.0# Then Ki(i) = 1.0E+20
                    Else
                        Ki(i) = 1.0E+20
                    End If
                Next

                i = 0
                Do
                    If Vz(i) <> 0 Then
                        Vs_ant(i) = Vs(i)
                        Vx_ant(i) = Vx(i)
                        Vx(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * L + 1)
                        If Double.IsNaN(Vx(i)) Then Vx(i) = 0.0#
                        If Ki(i) <> 0 Then Vs(i) = Vx(i) / Ki(i) Else Vs(i) = Vz(i)
                    Else
                        Vy(i) = 0
                        Vx(i) = 0
                    End If
                    i += 1
                Loop Until i = n + 1

                i = 0
                soma_x = 0
                soma_s = 0
                Do
                    soma_x = soma_x + Vx(i)
                    soma_s = soma_s + Vs(i)
                    i = i + 1
                Loop Until i = n + 1
                i = 0
                Do
                    If soma_x > 0.0# Then Vx(i) = Vx(i) / soma_x
                    If soma_s > 0.0# Then Vs(i) = Vs(i) / soma_s
                    i = i + 1
                Loop Until i = n + 1

                Dim e1 As Double = 0
                Dim e2 As Double = 0
                Dim e3 As Double = 0
                i = 0
                Do
                    e1 = e1 + (Vx(i) - Vx_ant(i))
                    e2 = e2 + (Vs(i) - Vs_ant(i))
                    i = i + 1
                Loop Until i = n + 1

                e3 = (L - Lant)

                If Double.IsNaN(Math.Abs(e1) + Math.Abs(e2)) Then

                    Throw New Exception(Calculator.GetLocalString("PropPack_FlashError"))

                ElseIf Math.Abs(e3) < 0.0000000001 And ecount > 0 Then

                    convergiu = 1

                    Exit Do

                Else

                    Lant = L

                    F = 0.0#
                    Dim dF = 0.0#
                    i = 0
                    Do
                        If Vz(i) > 0 Then
                            F = F + Vz(i) * (Ki(i) - 1) / (1 + L * (Ki(i) - 1))
                            dF = dF - Vz(i) * (Ki(i) - 1) ^ 2 / (1 + L * (Ki(i) - 1)) ^ 2
                        End If
                        i = i + 1
                    Loop Until i = n + 1

                    If Abs(F) < 0.000001 Then Exit Do

                    L = -0.7 * F / dF + L

                End If

                S = 1 - L

                If L > 1 Then
                    L = 1
                    S = 0
                    i = 0
                    Do
                        Vx(i) = Vz(i)
                        i = i + 1
                    Loop Until i = n + 1
                ElseIf L < 0 Then
                    L = 0
                    S = 1
                    i = 0
                    Do
                        Vs(i) = Vz(i)
                        i = i + 1
                    Loop Until i = n + 1
                End If

                ecount += 1

                If Double.IsNaN(L) Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashTPVapFracError"))
                If ecount > maxit_e Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2"))

                WriteDebugInfo("PT Flash [NL-SLE]: Iteration #" & ecount & ", LF = " & L)

                If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

            Loop Until convergiu = 1

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PT Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & F)

out:        Return New Object() {L, V, Vx, Vy, ecount, 0.0#, PP.RET_NullVector, S, Vs}

        End Function

        Function Flash_SL(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_SL", Name & " (SLE Flash)", "Pressure-Temperature Solid-Liquid Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine tries to find the compositions of liquid and solid phases at equilibrium.")

            IObj?.Paragraphs.Add("During the convergence process, solubility is checked for compounds in the liquid phase through the following equilibrium relation:")

            IObj?.Paragraphs.Add("<m>-\ln x_i^L\gamma_i^L= \frac{\Delta h_{m,i}}{RT}\left(1-\frac{T}{T_{m,i}}\right)-\frac{\Delta c_{P,i}(T_{m,i}-T)}{RT}+\frac{\Delta c_{P,i}}{R}\ln \frac{T_m}{T}</m>")

            IObj?.Paragraphs.Add("where <mi>x_i^L</mi> is the compound mole fraction in the liquid phase, <mi>\gamma _i^L</mi> is the activity coefficient of the compound in the liquid phase, <mi>T_{m,i}</mi> is the compound's melting point and <mi>\Delta c_{P,i}</mi> is the heat capacity difference of the compound between liquid and solid states.")

            'This flash is used to calculate the solid/liquid equilibrium at given pressure and temperature
            'Input parameters:  global mole fractions Vz
            'Result Parameters: number of moles in each phase

            Dim MaxError As Double = 0.0000001

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date

            d1 = Date.Now
            n = Vz.Length - 1

            Dim Vx(n), Vs(n), MaxAct(n), MaxX(n), MaxLiquPhase(n), Tf(n), Hf(n), Tc(n), ActCoeff(n), VnL(n), VnS(n), Vp(n) As Double
            Dim L, L_old, SF, SLP As Double
            Dim cpl(n), cps(n), dCp(n) As Double
            Dim Vn(n) As String
            Dim constprop As Interfaces.ICompoundConstantProperties

            Vx = Vz.Clone 'assuming initially only liquids exist
            Tf = PP.RET_VTF 'Fusion temperature
            Hf = PP.RET_VHF 'Enthalpy of fusion
            Tc = PP.RET_VTC 'Critical Temperature

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Critical Temperatures: {0} K", Tc.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Fusion Temperatures: {0} K", Tf.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Fusion Enthalpies: {0} kJ/mol", Hf.ToMathArrayString))

            If Vz.MaxY = 1.0# Then 'only a single component
                ecount = 0
                For i = 0 To n
                    If Vz(i) = 1 Then
                        If T > Tf(i) Then
                            'above melting temperature, only liquid
                            L = 1
                            L_old = L
                            Vx = Vz.Clone
                            Vs = PP.RET_NullVector
                            GoTo out
                        Else
                            'below melting temperature, only solid
                            L = 0
                            L_old = L
                            Vs = Vz.Clone
                            Vx = PP.RET_NullVector
                            GoTo out
                        End If
                    End If
                Next
            End If

            Vn = PP.RET_VNAMES()
            For i = 0 To n
                constprop = PP.CurrentMaterialStream.Phases(0).Compounds(Vn(i)).ConstantProperties
                IObj?.SetCurrent
                cpl(i) = PP.AUX_LIQ_Cpi(constprop, Tf(i))
                IObj?.SetCurrent
                cps(i) = PP.AUX_SolidHeatCapacity(constprop, Tf(i))
                'ignoring heat capacity difference due to issues with DWSIM characterization
                dCp(i) = 0.0# '(cpl(i) - cps(i)) * constprop.Molar_Weight
            Next

            'Calculate max activities for solubility of solids
            For i = 0 To n
                MaxAct(i) = Exp(-Hf(i) * 1000 / 8.31446 / T * (1 - T / Tf(i)) - dCp(i) / 8.31446 * ((T - Tf(i)) / T + Log(Tf(i) / T)))
                IObj?.SetCurrent
                Vp(i) = PP.AUX_PVAPi(i, T)
            Next

            IObj?.Paragraphs.Add(String.Format("<h2>Calculations</h2>"))

            L = 1
            Do

                ecount += 1

                IObj?.Paragraphs.Add(String.Format("<h3>Loop {0}</h3>", ecount))

                IObj?.SetCurrent
                ActCoeff = PP.DW_CalcFugCoeff(Vx, T, P, State.Liquid).MultiplyConstY(P).DivideY(Vp)
                MaxX = MaxAct.DivideY(ActCoeff)
                For i = 0 To n
                    'Supercritical gases are put to liquid phase
                    If T > Tc(i) Then MaxX(i) = 1
                    'If compound is in forced solids list, put it in solid phase
                    If PP.ForcedSolids.Contains(Vn(i)) Then MaxX(i) = 0.0
                Next

                IObj?.Paragraphs.Add(String.Format("Activity coefficients: {0}", ActCoeff.ToMathArrayString))
                IObj?.Paragraphs.Add(String.Format("Maximum solubilities (mole fractions): {0}", MaxX.ToMathArrayString))

                MaxLiquPhase = Vz.DivideY(MaxX)
                SF = 0
                For i = 0 To n
                    If MaxLiquPhase(i) > 0.0001 Then SF += MaxX(i)
                Next
                If SF < 1 Then
                    'only solid remaining
                    Vx = PP.RET_NullVector
                    Vs = Vz.Clone
                    L = 0
                    L_old = 0
                    Exit Do
                End If

                VnL = PP.RET_NullVector
                VnS = PP.RET_NullVector
                Vx = PP.RET_NullVector

                L_old = L

                For i = 0 To n
                    If Vz(i) > MaxX(i) Then
                        Vx(i) = MaxX(i) 'Component fraction above max solubility. -> fix fraction to max solubility
                    Else
                        VnL(i) = Vz(i) 'Component fraction below max solubility. -> put to liquid completely
                    End If
                Next

                SLP = VnL.SumY 'Sum moles of components in liquid phase
                SF = Vx.SumY 'Sum mole fractions of components fixed by max solubility
                If 1 - SLP < 0.00000001 Then SLP = 1
                L = SLP / (1 - SF)

                If L >= 1 Then
                    'all components are below max solubility, only liquid left
                    Vx = Vz.Clone
                    Vs = PP.RET_NullVector
                    Exit Do
                End If

                'calculate moles in liquid phase of components above max solubility
                For i = 0 To n
                    If Vz(i) > MaxX(i) Then
                        VnL(i) = MaxX(i) * L
                    End If
                    VnS(i) = Vz(i) - VnL(i)
                Next

                If L > 0 And MaxX.SumY > 1 Then
                    Vx = VnL.NormalizeY
                    Vs = VnS.NormalizeY
                Else
                    'only solid remaining
                    Vx = PP.RET_NullVector
                    Vs = Vz.Clone
                    L = 0
                    L_old = 0
                    Exit Do
                End If

                IObj?.Paragraphs.Add(String.Format("Current estimates for liquid phase composition: {0}", Vx.ToMathArrayString))
                IObj?.Paragraphs.Add(String.Format("Current estimates for solid phase composition: {0}", Vs.ToMathArrayString))

                IObj?.Paragraphs.Add(String.Format("Current estimates for liquid phase mole fraction: {0}", L))
                IObj?.Paragraphs.Add(String.Format("Current estimates for solid phase mole fraction: {0}", 1 - L))

            Loop Until Abs(L - L_old) < MaxError

out:        d2 = Date.Now

            IObj?.Paragraphs.Add(String.Format("<h2>Results</h2>"))

            IObj?.Paragraphs.Add(String.Format("Final liquid phase composition: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final solid phase composition: {0}", Vs.ToMathArrayString))

            IObj?.Paragraphs.Add(String.Format("Final liquid phase mole fraction: {0}", L))
            IObj?.Paragraphs.Add(String.Format("Final solid phase mole fraction: {0}", 1 - L))

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & (d2 - d1).TotalMilliseconds & " ms. Error function value: " & Abs(L - L_old))

            IObj?.Close()

            Return New Object() {L, 1 - L, 0.0#, Vx, Vs, L - L_old, ecount, d2 - d1}

        End Function

        Public Function Flash_PT_NL(ByVal Vz0 As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PT", Name & " (PT Flash)", "Pressure-Temperature Flash Algorithm Routine", True)

            IObj?.Paragraphs.Add("This routine tries to find the compositions of vapor, liquid and solid phases at equilibrium by solving the Rachford-Rice equation using a newton convergence approach.")

            IObj?.Paragraphs.Add("The Rachford-Rice equation is")

            IObj?.Paragraphs.Add("<math>\sum_i\frac{z_i \, (K_i - 1)}{1 + \beta \, (K_i - 1)}= 0</math>")

            IObj?.Paragraphs.Add("where:")

            IObj?.Paragraphs.Add("<math_inline>z_{i}</math_inline> is the mole fraction of component i in the feed liquid (assumed to be known);")
            IObj?.Paragraphs.Add("<math_inline>\beta</math_inline> is the fraction of feed that is vaporised;")
            IObj?.Paragraphs.Add("<math_inline>K_{i}</math_inline> is the equilibrium constant of component i.")

            IObj?.Paragraphs.Add("The equilibrium constants K<sub>i</sub> are in general functions of many parameters, though the most important is arguably temperature; they are defined as:")

            IObj?.Paragraphs.Add("<math>y_i = K_i \, x_i</math>")

            IObj?.Paragraphs.Add("where:")

            IObj?.Paragraphs.Add("<math_inline>x_i</math_inline> is the mole fraction of component i in liquid phase;")
            IObj?.Paragraphs.Add("<math_inline>y_i</math_inline> is the mole fraction of component i in gas phase.")

            IObj?.Paragraphs.Add("Once the Rachford-Rice equation has been solved for <math_inline>\beta</math_inline>, the compositions x<sub>i</sub> and y<sub>i</sub> can be immediately calculated as:")

            IObj?.Paragraphs.Add("<math>x_i =\frac{z_i}{1+\beta(K_i-1)}\\y_i=K_i\,x_i</math>")

            IObj?.Paragraphs.Add("The Rachford - Rice equation can have multiple solutions for <math_inline>\beta</math_inline>, at most one of which guarantees that all <math_inline>x_i</math_inline> and <math_inline>y_i</math_inline> will be positive. In particular, if there is only one <math_inline>\beta</math_inline> for which:")
            IObj?.Paragraphs.Add("<math>\frac{1}{1-K_\text{max}}=\beta_\text{min}<\beta<\beta_\text{max}=\frac{1}{1-K_\text{min}}</math>")
            IObj?.Paragraphs.Add("then that <math_inline>\beta</math_inline> is the solution; if there are multiple  such <math_inline>\beta</math_inline>s, it means that either <math_inline>K_{max}<1</math_inline> or <math_inline>K_{min}>1</math_inline>, indicating respectively that no gas phase can be sustained (and therefore <math_inline>\beta=0</math_inline>) or conversely that no liquid phase can exist (and therefore <math_inline>\beta=1</math_inline>).")

            IObj?.Paragraphs.Add("DWSIM initializes the current calculation with ideal K-values estimated from vapor pressure data for each compound, or by using previously calculated values from an earlier solution.")

            IObj?.Paragraphs.Add("During the convergence process, solubility is checked for compounds in the liquid phase through the following equilibrium relation:")

            IObj?.Paragraphs.Add("<m>-\ln x_i^L\gamma_i^L= \frac{\Delta h_{m,i}}{RT}\left(1-\frac{T}{T_{m,i}}\right)-\frac{\Delta c_{P,i}(T_{m,i}-T)}{RT}+\frac{\Delta c_{P,i}}{R}\ln \frac{T_m}{T}</m>")

            IObj?.Paragraphs.Add("where <mi>x_i^L</mi> is the compound mole fraction in the liquid phase, <mi>\gamma _i^L</mi> is the activity coefficient of the compound in the liquid phase, <mi>T_{m,i}</mi> is the compound's melting point and <mi>\Delta c_{P,i}</mi> is the heat capacity difference of the compound between liquid and solid states.")

            Dim i, n, ecount, gcount As Integer
            Dim Pb, Pd, Pmin, Pmax, Px As Double
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, S, Vant As Double
            Dim GL_old, GS_old, GV_old As Double
            Dim GlobalConv As Boolean = False

            Dim SVE As Boolean = False

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz0.Length - 1

            Dim Vx(n), Vy(n), Vs(n), Vmix(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), Ki_ant(n), Vz(n) As Double

            Vz = Vz0.Clone

            'Calculate Ki`s
            If Not ReuseKI Then
                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = Vp(i) / P
                    i += 1
                Loop Until i = n + 1
            Else
                For i = 0 To n
                    Vp(i) = PP.AUX_PVAPi(i, T)
                    Ki(i) = PrevKi(i)
                Next
            End If

            'initially put all into liquid phase
            Vx = Vz.Clone
            S = 0.0
            L = 1.0
            V = 0.0

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))

            IObj?.Paragraphs.Add(String.Format("<h2>Calculated Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Initial estimate for V: {0}", V))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for L: {0}", L))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for S: {0}", S))

            'do flash calculation iterations
            Do

                GL_old = L
                GV_old = V
                GS_old = S
                gcount += 1

                IObj?.SetCurrent()

                Dim IObj2 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                Inspector.Host.CheckAndAdd(IObj2, "", "Flash_PT", "PT SVLE Internal Loop Iteration", "Pressure-Temperature Flash Algorithm Convergence Iteration Step", True)

                IObj2?.Paragraphs.Add(String.Format("This is the SVLE convergence loop iteration #{0}. DWSIM will use the current values of y, x and s to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                If V < 1.0 Then

                    'there is some liquid or solid

                    '================================================
                    '== mix solid and liquid phase ==================
                    '================================================
                    Vmix = Vs.MultiplyConstY(S)
                    Vmix = Vmix.AddY(Vx.MultiplyConstY(L))
                    Vz = Vmix.NormalizeY

                    '================================================
                    '== Do initial SLE flash to precipitate solids ==
                    '================================================
                    Dim SL_Result As Object
                    IObj2?.SetCurrent
                    SL_Result = Flash_SL(Vz, P, T, PP)
                    Vx = SL_Result(3)
                    Vs = SL_Result(4)

                    'calculate global phase fractions
                    L = SL_Result(0) * (1 - V)
                    S = SL_Result(1) * (1 - V)
                End If

                'only solids and/or vapour left

                If L = 0.0 Then
                    SVE = True
                    GoTo out2
                Else
                    SVE = False
                End If

                '================================================
                '== mix vapour and liquid phase =================
                '================================================
                Vmix = Vy.MultiplyConstY(V)
                Vmix = Vmix.AddY(Vx.MultiplyConstY(L))
                Vz = Vmix.NormalizeY

                '================================================
                '== Do VLE flash ================================
                '================================================

                'Estimate V
                If T > MathEx.Common.Max(PP.RET_VTC, Vz) Then
                    Vy = Vz
                    V = 1
                    L = 0
                    GoTo out
                End If

                i = 0
                Px = 0
                Do
                    If Vp(i) <> 0.0# Then Px = Px + (Vz(i) / Vp(i))
                    i = i + 1
                Loop Until i = n + 1
                Px = 1 / Px
                Pmin = Px
                Pmax = SumY(Vz.MultiplyY(Vp))
                Pb = Pmax
                Pd = Pmin

                If Abs(Pb - Pd) / Pb < 0.0000001 Then
                    'one comp only
                    If Px <= P Then
                        L = 1
                        V = 0
                        Vx = Vz
                        GoTo out
                    Else
                        L = 0
                        V = 1
                        Vy = Vz
                        GoTo out
                    End If
                End If

                Dim Vmin, Vmax, g As Double
                Vmin = 1.0#
                Vmax = 0.0#
                For i = 0 To n
                    If (Ki(i) * Vz(i) - 1) / (Ki(i) - 1) < Vmin Then Vmin = (Ki(i) * Vz(i) - 1) / (Ki(i) - 1)
                    If (1 - Vz(i)) / (1 - Ki(i)) > Vmax Then Vmax = (1 - Vz(i)) / (1 - Ki(i))
                Next

                If Vmin < 0.0# Then Vmin = 0.0#
                If Vmin = 1.0# Then Vmin = 0.0#
                If Vmax = 0.0# Then Vmax = 1.0#
                If Vmax > 1.0# Then Vmax = 1.0#

                V = (Vmin + Vmax) / 2

                g = 0.0#
                For i = 0 To n
                    g += Vz(i) * (Ki(i) - 1) / (V + (1 - V) * Ki(i))
                Next

                If g > 0 Then Vmin = V Else Vmax = V

                V = Vmin + (Vmax - Vmin) / 2
                L = 1 - V

                If n = 0 Then
                    If Vp(0) <= P Then
                        L = 1
                        V = 0
                    Else
                        L = 0
                        V = 1
                    End If
                End If

                i = 0
                Do
                    If Vz(i) <> 0 Then
                        Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                        If Ki(i) <> 0 Then Vx(i) = Vy(i) / Ki(i) Else Vx(i) = Vz(i)
                        If Vy(i) < 0 Then Vy(i) = 0
                        If Vx(i) < 0 Then Vx(i) = 0
                    Else
                        Vy(i) = 0
                        Vx(i) = 0
                    End If
                    i += 1
                Loop Until i = n + 1

                Vy_ant = Vy.Clone
                Vx_ant = Vx.Clone

                Vy = Vz.MultiplyY(Ki).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1))
                For i = 0 To n
                    If Double.IsNaN(Vy(i)) Then Vy(i) = 0
                Next
                Vx = Vy.DivideY(Ki)

                Vx = Vx.NormalizeY
                Vy = Vy.NormalizeY

                Dim convergiu As Integer = 0
                Dim F, dF, e1, e2, e3 As Double
                ecount = 0

                Do

                    IObj2?.SetCurrent()

                    Dim IObj3 As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

                    Inspector.Host.CheckAndAdd(IObj3, "", "Flash_PT", "PT Flash VLE Newton Iteration", "Pressure-Temperature Flash Algorithm Convergence Iteration Step")

                    IObj3?.Paragraphs.Add(String.Format("This is the VLE Newton convergence loop iteration #{0}. DWSIM will use the current values of y and x to calculate fugacity coefficients and update K using the Property Package rigorous models.", ecount))

                    IObj3?.SetCurrent()

                    Ki_ant = Ki.Clone
                    Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                    IObj3?.Paragraphs.Add(String.Format("K values where updated. Current values: {0}", Ki.ToMathArrayString))

                    Vy_ant = Vy.Clone
                    Vx_ant = Vx.Clone

                    Vy = Vz.MultiplyY(Ki).DivideY(Ki.AddConstY(-1).MultiplyConstY(V).AddConstY(1))
                    For i = 0 To n
                        If Double.IsNaN(Vy(i)) Then Vy(i) = 0
                    Next
                    Vx = Vy.DivideY(Ki)

                    Vx = Vx.NormalizeY
                    Vy = Vy.NormalizeY

                    IObj3?.Paragraphs.Add(String.Format("y values (vapor phase composition) where updated. Current values: {0}", Vy.ToMathArrayString))
                    IObj3?.Paragraphs.Add(String.Format("x values (liquid phase composition) where updated. Current values: {0}", Vx.ToMathArrayString))

                    e1 = Vx.SubtractY(Vx_ant).AbsSumY
                    e2 = Vy.SubtractY(Vy_ant).AbsSumY

                    e3 = (V - Vant)

                    IObj3?.Paragraphs.Add(String.Format("Current Vapor Fraction (<math_inline>\beta</math_inline>) error: {0}", e3))

                    If Double.IsNaN(e1 + e2) Then

                        Throw New Exception(Calculator.GetLocalString("PropPack_FlashError"))

                    ElseIf Math.Abs(e3) < 0.0000000001 And ecount > 0 Then

                        convergiu = 1

                        Exit Do

                    Else

                        Vant = V

                        F = 0.0#
                        dF = 0.0#
                        i = 0
                        Do
                            If Vz(i) > 0 Then
                                F = F + Vz(i) * (Ki(i) - 1) / (1 + V * (Ki(i) - 1))
                                dF = dF - Vz(i) * (Ki(i) - 1) ^ 2 / (1 + V * (Ki(i) - 1)) ^ 2
                            End If
                            i = i + 1
                        Loop Until i = n + 1

                        IObj3?.Paragraphs.Add(String.Format("Current value of the Rachford-Rice error function: {0}", F))

                        If Abs(F) < etol / 100 Then Exit Do

                        V = -F / dF + Vant

                        IObj3?.Paragraphs.Add(String.Format("Updated Vapor Fraction (<math_inline>\beta</math_inline>) value: {0}", V))

                    End If

                    If V < 0.0# Then V = 0.0#
                    If V > 1.0# Then V = 1.0#

                    L = 1 - V

                    ecount += 1

                    If Double.IsNaN(V) Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashTPVapFracError"))
                    If ecount > maxit_e Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2"))

                    WriteDebugInfo("PT Flash [NL-SLE]: Iteration #" & ecount & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                    IObj3?.Close()

                Loop Until convergiu = 1

out:            'calculate global phase fractions
                L = L * (1 - S)
                V = V * (1 - S)

                If gcount > maxit_e Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2"))

out2:           If (Math.Abs(GL_old - L) < 0.0000005) And (Math.Abs(GV_old - V) < 0.0000005) And (Math.Abs(GS_old - S) < 0.0000005) Then GlobalConv = True

                IObj2?.Paragraphs.Add(String.Format("Current estimates for liquid phase composition: {0}", Vx.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Current estimates for solid phase composition: {0}", Vs.ToMathArrayString))
                IObj2?.Paragraphs.Add(String.Format("Current estimates for vapor phase composition: {0}", Vy.ToMathArrayString))

                IObj2?.Paragraphs.Add(String.Format("Current estimates for liquid phase mole fraction: {0}", L))
                IObj2?.Paragraphs.Add(String.Format("Current estimates for solid phase mole fraction: {0}", S))
                IObj2?.Paragraphs.Add(String.Format("Current estimates for vapor phase mole fraction: {0}", V))

                IObj2?.Close()

            Loop Until GlobalConv

            If SVE Then

                'solid-vapor equilibria

                Dim result = New NestedLoops().Flash_PT(Vz0, P, T, PP)

                V = result(1)
                Vy = result(3)

                Dim SL_Result = Flash_SL(result(2), P, T, PP)

                Vx = SL_Result(3)
                Vs = SL_Result(4)

                L = SL_Result(0) * (1 - V)
                S = SL_Result(1) * (1 - V)

            End If

            IObj?.Paragraphs.Add("The algorithm converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("Final liquid phase composition: {0}", Vx.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final solid phase composition: {0}", Vs.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Final vapor phase composition: {0}", Vy.ToMathArrayString))

            IObj?.Paragraphs.Add(String.Format("Final liquid phase mole fraction: {0}", L))
            IObj?.Paragraphs.Add(String.Format("Final solid phase mole fraction: {0}", S))
            IObj?.Paragraphs.Add(String.Format("Final vapor phase mole fraction: {0}", V))

            IObj?.Close()

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PT Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds)

            Return New Object() {L, V, Vx, Vy, ecount, 0.0#, PP.RET_NullVector, S, Vs}

        End Function

        Public Function Flash_PT_E(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object


            Dim d1, d2 As Date, dt As TimeSpan

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            'This flash algorithm is for Electrolye/Salt systems with Water as the single solvent.
            'The vapor and solid phases are considered to be ideal.
            'Chemical equilibria is calculated using the reactions enabled in the default reaction set.

            Dim n As Integer = CompoundProperties.Count - 1
            Dim activcoeff(n) As Double
            Dim i As Integer

            'Vnf = feed molar amounts (considering 1 mol of feed)
            'Vnl = liquid phase molar amounts
            'Vnv = vapor phase molar amounts
            'Vns = solid phase molar amounts
            'Vxl = liquid phase molar fractions
            'Vxv = vapor phase molar fractions
            'Vxs = solid phase molar fractions
            'V, S, L = phase molar amounts (F = 1 = V + S + L)
            Dim Vnf(n), Vnl(n), Vxl(n), Vxl_ant(n), Vns(n), Vxs(n), Vnv(n), Vxv(n), V, S, L, L_ant, Vp(n) As Double
            Dim sumN As Double = 0

            Vnf = Vz.Clone

            'calculate SLE.

            Dim ids As New List(Of String)
            For i = 0 To n
                ids.Add(CompoundProperties(i).Name)
                Vp(i) = PP.AUX_PVAPi(i, T)
            Next

            Vxl = Vz.Clone

            'initial estimates for L and S.

            L = 0.0#

            'calculate liquid phase activity coefficients.

            Dim ecount As Integer = 0
            Dim errfunc As Double = 0.0#

            Do



                For i = 0 To n
                    activcoeff(i) = activcoeff(i) * P / PP.AUX_PVAPi(ids(i), T)
                Next

                Dim Vxlmax(n) As Double

                'calculate maximum solubilities for solids/precipitates.

                For i = 0 To n
                    If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                        Vxlmax(i) = (1 / activcoeff(i)) * Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                        If Vxlmax(i) > 1 Then Vxlmax(i) = 1.0#
                    Else
                        Vxlmax(i) = 1.0#
                    End If
                Next

                'mass balance.

                Dim hassolids As Boolean = False

                S = 0.0#
                For i = 0 To n
                    If Vnf(i) > Vxlmax(i) Then
                        hassolids = True
                        Vxl(i) = Vxlmax(i)
                        S += Vnf(i) - Vxl(i) * L
                    End If
                Next

                'check for vapors
                V = 0.0#
                For i = 0 To n
                    If P < Vp(i) Then
                        V += Vnf(i)
                        Vxl(i) = 0
                        Vnv(i) = Vnf(i)
                    End If
                Next

                L_ant = L
                If hassolids Then L = 1 - S - V Else L = 1 - V

                For i = 0 To n
                    Vns(i) = Vnf(i) - Vxl(i) * L - Vnv(i)
                    Vnl(i) = Vxl(i) * L
                Next

                For i = 0 To n
                    If Sum(Vnl) <> 0.0# Then Vxl(i) = Vnl(i) / Sum(Vnl) Else Vxl(i) = 0.0#
                    If Sum(Vns) <> 0.0# Then Vxs(i) = Vns(i) / Sum(Vns) Else Vxs(i) = 0.0#
                    If Sum(Vnv) <> 0.0# Then Vxv(i) = Vnv(i) / Sum(Vnv) Else Vxv(i) = 0.0#
                Next

                errfunc = Abs(L - L_ant) ^ 2

                If errfunc <= etol Then Exit Do

                If Double.IsNaN(S) Then Throw New Exception(Calculator.GetLocalString("PP_FlashTPSolidFracError"))
                If ecount > maxit_e Then Throw New Exception(Calculator.GetLocalString("PP_FlashMaxIt2"))

                ecount += 1

                If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

            Loop

            'return flash calculation results.

            d2 = Date.Now

            dt = d2 - d1


            WriteDebugInfo("PT Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms. Error function value: " & errfunc)

out:        Return New Object() {L, V, Vxl, Vxv, ecount, 0.0#, PP.RET_NullVector, S, Vxs}

        End Function

        Public Overrides Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PH", Name & " (PH Flash - Fast Mode)", "Pressure-Enthalpy Flash Algorithm Routine (Fast Mode)")

            IObj?.Paragraphs.Add("The PH Flash in fast mode uses two nested loops (hence the name) to calculate temperature and phase distribution. 
                                    The external one converges the temperature, while the internal one finds the phase distribution for the current temperature estimate in the external loop.
                                    The algorithm converges when the calculated overall enthalpy for the tentative phase distribution and temperature matches the specified one.")

            IObj?.SetCurrent()

            CompoundProperties = PP.DW_GetConstantProperties

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1), Vs(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, T, S, Pf As Double

            d1 = Date.Now

            n = Vz.Length - 1

            PP = PP
            Hf = H
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n), Vs(n)

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tsup, Tinf

            If Tref <> 0 Then
                Tinf = Tref - 250
                Tsup = Tref + 250
            Else
                Tinf = PP.RET_VTF.MultiplyY(Vz).SumY * 0.3
                Tsup = 5000
            End If
            If Tinf < 20 Then Tinf = 20

            Dim bo As New BrentOpt.Brent
            bo.DefineFuncDelegate(AddressOf Herror)
            WriteDebugInfo("PH Flash: Starting calculation for " & Tinf & " <= T <= " & Tsup)

            Dim fx, fx2, dfdx, x1 As Double

            Dim cnt As Integer = 0

            If Tref = 0 Then Tref = 300.0#

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Enthalpy: {0} kJ/kg", H))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", Tref))

            x1 = Tref
            Try
                Do
                    IObj?.SetCurrent()
                    fx = Herror(x1, {P, Vz, PP})
                    IObj?.SetCurrent()
                    fx2 = Herror(x1 + 1, {P, Vz, PP})
                    If Abs(fx) < etol Then Exit Do
                    dfdx = (fx2 - fx)
                    x1 = x1 - fx / dfdx
                    If x1 < 0 Then GoTo alt
                    cnt += 1
                Loop Until cnt > 50 Or Double.IsNaN(x1)
            Catch ex As Exception
                x1 = Double.NaN
            End Try
            If Double.IsNaN(x1) Or cnt > 50 Then
alt:            T = bo.BrentOpt(Tinf, Tsup, 100, tolEXT, maxitEXT, {P, Vz, PP})
            Else
                T = x1
            End If

            'End If

            IObj?.SetCurrent()
            Dim tmp As Object = Flash_PT(Vz, P, T, PP)

            L = tmp(0)
            V = tmp(1)
            S = tmp(7)
            Vx = tmp(2)
            Vy = tmp(3)
            Vs = tmp(8)
            ecount = tmp(4)

            For i = 0 To n
                Ki(i) = Vy(i) / Vx(i)
            Next

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PH Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("The PH Flash algorithm converged in {0} iterations. Final Temperature value: {1} K", cnt, T))

            IObj?.Close()

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, S, Vs}

        End Function

        Public Overrides Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Flash_PS", Name & " (PS Flash - Fast Mode)", "Pressure-Enthalpy Flash Algorithm Routine (Fast Mode)")

            IObj?.Paragraphs.Add("The PH Flash in fast mode uses two nested loops (hence the name) to calculate temperature and phase distribution. 
                                    The external one converges the temperature, while the internal one finds the phase distribution for the current temperature estimate in the external loop.
                                    The algorithm converges when the calculated overall enthalpy for the tentative phase distribution and temperature matches the specified one.")

            IObj?.SetCurrent()

            CompoundProperties = PP.DW_GetConstantProperties

            Dim doparallel As Boolean = Settings.EnableParallelProcessing

            Dim Vn(1) As String, Vx(1), Vy(1), Vx_ant(1), Vy_ant(1), Vp(1), Ki(1), Ki_ant(1), fi(1), Vs(1) As Double
            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, V, Ss, T, Pf As Double

            d1 = Date.Now

            n = Vz.Length - 1

            PP = PP
            Sf = S
            Pf = P

            ReDim Vn(n), Vx(n), Vy(n), Vx_ant(n), Vy_ant(n), Vp(n), Ki(n), fi(n), Vs(n)

            Vn = PP.RET_VNAMES()
            fi = Vz.Clone

            Dim maxitINT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_Internal_Iterations)
            Dim maxitEXT As Integer = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Maximum_Number_Of_External_Iterations)
            Dim tolINT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            Dim tolEXT As Double = Me.FlashSettings(Interfaces.Enums.FlashSetting.PHFlash_External_Loop_Tolerance).ToDoubleFromInvariant

            Dim Tsup, Tinf ', Ssup, Sinf

            If Tref <> 0 Then
                Tinf = Tref - 200
                Tsup = Tref + 200
            Else
                Tinf = PP.RET_VTF.MultiplyY(Vz).SumY * 0.3
                Tsup = 10000
            End If
            If Tinf < 20 Then Tinf = 20
            Dim bo As New BrentOpt.Brent
            bo.DefineFuncDelegate(AddressOf Serror)
            WriteDebugInfo("PS Flash: Starting calculation for " & Tinf & " <= T <= " & Tsup)

            Dim fx, fx2, dfdx, x1 As Double

            Dim cnt As Integer = 0

            If Tref = 0 Then Tref = 298.15
            x1 = Tref

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Entropy: {0} kJ/kg", S))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", PP.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Initial estimate for T: {0} K", Tref))

            Try
                Do
                    IObj?.SetCurrent()
                    fx = Serror(x1, {P, Vz, PP})
                    IObj?.SetCurrent()
                    fx2 = Serror(x1 + 1, {P, Vz, PP})
                    If Abs(fx) < etol Then Exit Do
                    dfdx = (fx2 - fx)
                    x1 = x1 - fx / dfdx
                    If x1 < 0 Then GoTo alt
                    cnt += 1
                Loop Until cnt > 50 Or Double.IsNaN(x1)
            Catch ex As Exception
                x1 = Double.NaN
            End Try
            If Double.IsNaN(x1) Or cnt > 50 Then
                IObj?.SetCurrent()
alt:            T = bo.BrentOpt(Tinf, Tsup, 100, tolEXT, maxitEXT, {P, Vz, PP})
            Else
                T = x1
            End If

            IObj?.SetCurrent()
            Dim tmp As Object = Flash_PT(Vz, P, T, PP)

            L = tmp(0)
            V = tmp(1)
            Ss = tmp(7)
            Vx = tmp(2)
            Vy = tmp(3)
            Vs = tmp(8)
            ecount = tmp(4)

            For i = 0 To n
                Ki(i) = Vy(i) / Vx(i)
            Next

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PS Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            IObj?.Paragraphs.Add(String.Format("The PS Flash algorithm converged in {0} iterations. Final Temperature value: {1} K", cnt, T))

            IObj?.Close()

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, Ss, Vs}

        End Function

        Public Overrides Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim d1, d2 As Date, dt As TimeSpan

            d1 = Date.Now

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("TV Flash [NL-SLE]: Converged in " & 0 & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {0.0#, 0.0#, PP.RET_NullVector, PP.RET_NullVector, 0, 0, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Function SolidFractionError(x As Double, otherargs As Object)
            Dim res As Object = Me.Flash_PT(otherargs(1), otherargs(2), x, otherargs(3))
            Dim val As Double = (1 - otherargs(0)) - res(7)

            Return val

        End Function

        Function OBJ_FUNC_PH_FLASH(ByVal T As Double, ByVal H As Double, ByVal P As Double, ByVal Vz As Object, ByVal pp As PropertyPackage) As Object

            Dim tmp As Object
            tmp = Me.Flash_PT(Vz, P, T, pp)
            Dim L, V, S, Vx(), Vy(), Vs(), _Hv, _Hl, _Hs As Double

            Dim n = Vz.Length - 1

            L = tmp(0)
            V = tmp(1)
            S = tmp(7)
            Vx = tmp(2)
            Vy = tmp(3)
            Vs = tmp(8)

            _Hv = 0
            _Hl = 0
            _Hs = 0

            Dim mmg, mml, mms As Double
            If V > 0 Then _Hv = pp.DW_CalcEnthalpy(Vy, T, P, State.Vapor)
            If L > 0 Then _Hl = pp.DW_CalcEnthalpy(Vx, T, P, State.Liquid)
            If S > 0 Then _Hs = pp.DW_CalcEnthalpy(Vs, T, P, State.Solid)
            mmg = pp.AUX_MMM(Vy)
            mml = pp.AUX_MMM(Vx)
            mms = pp.AUX_MMM(Vs)

            Dim herr As Double = Hf - (mmg * V / (mmg * V + mml * L + mms * S)) * _Hv - (mml * L / (mmg * V + mml * L + mms * S)) * _Hl - (mms * S / (mmg * V + mml * L + mms * S)) * _Hs
            OBJ_FUNC_PH_FLASH = herr

            WriteDebugInfo("PH Flash [NL-SLE]: Current T = " & T & ", Current H Error = " & herr)

        End Function

        Function OBJ_FUNC_PS_FLASH(ByVal T As Double, ByVal S As Double, ByVal P As Double, ByVal Vz As Object, ByVal pp As PropertyPackage) As Object

            Dim tmp As Object
            tmp = Me.Flash_PT(Vz, P, T, pp)
            Dim L, V, Ssf, Vx(), Vy(), Vs(), _Sv, _Sl, _Ss As Double

            Dim n = Vz.Length - 1

            L = tmp(0)
            V = tmp(1)
            Ssf = tmp(7)
            Vx = tmp(2)
            Vy = tmp(3)
            Vs = tmp(8)

            _Sv = 0
            _Sl = 0
            _Ss = 0
            Dim mmg, mml, mms As Double

            If V > 0 Then _Sv = pp.DW_CalcEntropy(Vy, T, P, State.Vapor)
            If L > 0 Then _Sl = pp.DW_CalcEntropy(Vx, T, P, State.Liquid)
            If Ssf > 0 Then _Ss = pp.DW_CalcEntropy(Vs, T, P, State.Solid)
            mmg = pp.AUX_MMM(Vy)
            mml = pp.AUX_MMM(Vx)
            mms = pp.AUX_MMM(Vs)

            Dim serr As Double = Sf - (mmg * V / (mmg * V + mml * L + mms * Ssf)) * _Sv - (mml * L / (mmg * V + mml * L + mms * Ssf)) * _Sl - (mms * Ssf / (mmg * V + mml * L + mms * Ssf)) * _Ss
            OBJ_FUNC_PS_FLASH = serr

            WriteDebugInfo("PS Flash [NL-SLE]: Current T = " & T & ", Current S Error = " & serr)

        End Function

        Function Herror(ByVal Tt As Double, ByVal otherargs As Object) As Double
            Return OBJ_FUNC_PH_FLASH(Tt, Hf, otherargs(0), otherargs(1), otherargs(2))
        End Function

        Function Serror(ByVal Tt As Double, ByVal otherargs As Object) As Double
            Return OBJ_FUNC_PS_FLASH(Tt, Sf, otherargs(0), otherargs(1), otherargs(2))
        End Function


        Public Overrides Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object


            Dim i, n, ecount, gcount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, S, Lf, Vf, Vint, T, Tf, deltaT As Double
            Dim e1 As Double
            Dim AF As Double = 1
            Dim GL_old, GS_old, GV_old As Double

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            Vf = V
            L = 1 - V
            Lf = 1 - Vf
            Tf = T
            Vint = V
            GL_old = L
            GV_old = V
            GS_old = 0

            Dim Vn(n) As String, Vx(n), Vy(n), Vs(n), Vmix(n), Vx_ant(n), Vy_ant(n), Vs_ant(n), Vp(n), Ki(n), fi(n) As Double
            Dim Vt(n), VTc(n), Tmin, Tmax, dFdT, Tsat(n) As Double

            Vn = PP.RET_VNAMES()
            VTc = PP.RET_VTC()
            fi = Vz.Clone

            Tmin = 0.0#
            Tmax = 0.0#

            If Tref = 0.0# Then
                i = 0
                Tref = 0.0#
                Do
                    Tref += 0.8 * Vz(i) * VTc(i)
                    Tmin += 0.1 * Vz(i) * VTc(i)
                    Tmax += 2.0 * Vz(i) * VTc(i)
                    i += 1
                Loop Until i = n + 1
            Else
                Tmin = Tref - 50
                Tmax = Tref + 50
            End If

            T = Tref

            'Calculate Ki`s

            If Not ReuseKI Then
                i = 0
                Do
                    Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                    Ki(i) = Vp(i) / P
                    i += 1
                Loop Until i = n + 1
            Else
                If Not PP.AUX_CheckTrivial(PrevKi) And Not Double.IsNaN(PrevKi(0)) Then
                    For i = 0 To n
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = PrevKi(i)
                    Next
                Else
                    i = 0
                    Do
                        Vp(i) = PP.AUX_PVAPi(Vn(i), T)
                        Ki(i) = Vp(i) / P
                        i += 1
                    Loop Until i = n + 1
                End If
            End If

            i = 0
            Do
                If Vz(i) <> 0 Then
                    Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * V + 1)
                    If Double.IsInfinity(Vy(i)) Then Vy(i) = 0.0#
                Else
                    Vy(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            Vy = Vy.NormalizeY()
            Vx = Vz.SubtractY(Vy.MultiplyConstY(V)).MultiplyConstY(1 / L)

            If PP.AUX_IS_SINGLECOMP(Vz) Then
                WriteDebugInfo("PV Flash [SLE]: Converged in 1 iteration.")
                T = 0
                For i = 0 To n
                    T += Vz(i) * PP.AUX_TSATi(P, i)
                Next
                Return New Object() {L, V, Vx, Vy, T, 0, Ki, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}
            End If

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Tant, fval As Double
            Dim chk As Boolean = False

            If V = 1.0# Then

                ecount = 0
                Do

                    marcador3 = 0

                    Dim cont_int = 0
                    Do

                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        marcador = 0
                        If stmp4_ant <> 0 Then
                            marcador = 1
                        End If
                        stmp4_ant = stmp4

                        If V = 0 Then
                            stmp4 = Ki.MultiplyY(Vx).SumY
                            'i = 0
                            'stmp4 = 0
                            'Do
                            '    stmp4 = stmp4 + Ki(i) * Vx(i)
                            '    i = i + 1
                            'Loop Until i = n + 1
                        Else
                            stmp4 = Vy.DivideY(Ki).SumY
                            'i = 0
                            'stmp4 = 0
                            'Do
                            '    stmp4 = stmp4 + Vy(i) / Ki(i)
                            '    i = i + 1
                            'Loop Until i = n + 1
                        End If

                        If V = 0 Then
                            Vy_ant = Vy.Clone
                            Vy = Ki.MultiplyY(Vx).MultiplyConstY(1 / stmp4)
                            'i = 0
                            'Do
                            '    Vy_ant(i) = Vy(i)
                            '    Vy(i) = Ki(i) * Vx(i) / stmp4
                            '    i = i + 1
                            'Loop Until i = n + 1
                        Else
                            Vx_ant = Vx.Clone
                            Vx = Vy.DivideY(Ki).MultiplyConstY(1 / stmp4)
                            'i = 0
                            'Do
                            '    Vx_ant(i) = Vx(i)
                            '    Vx(i) = (Vy(i) / Ki(i)) / stmp4
                            '    i = i + 1
                            'Loop Until i = n + 1
                        End If

                        marcador2 = 0
                        If marcador = 1 Then
                            If V = 0 Then
                                If Math.Abs(Vy(0) - Vy_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            Else
                                If Math.Abs(Vx(0) - Vx_ant(0)) < itol Then
                                    marcador2 = 1
                                End If
                            End If
                        End If

                        cont_int = cont_int + 1

                    Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                    Dim K1(n), K2(n), dKdT(n) As Double

                    K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                    K2 = PP.DW_CalcKvalue(Vx, Vy, T + 0.01, P)

                    dKdT = K2.SubtractY(K1).MultiplyConstY(1 / 0.01)
                    'For i = 0 To n
                    '    dKdT(i) = (K2(i) - K1(i)) / 0.01
                    'Next

                    fval = stmp4 - 1

                    ecount += 1

                    i = 0
                    dFdT = 0
                    Do
                        If V = 0 Then
                            dFdT = Vx.MultiplyY(dKdT).SumY
                            'dFdT = dFdT + Vx(i) * dKdT(i)
                        Else
                            dFdT = -Vy.DivideY(Ki).DivideY(Ki).MultiplyY(dKdT).SumY
                            'dFdT = dFdT - Vy(i) / (Ki(i) ^ 2) * dKdT(i)
                        End If
                        i = i + 1
                    Loop Until i = n + 1

                    Tant = T
                    deltaT = -fval / dFdT

                    If Abs(deltaT) > 0.1 * T Then
                        T = T + Sign(deltaT) * 0.1 * T
                    Else
                        T = T + deltaT
                    End If

                    WriteDebugInfo("PV Flash [SLE]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                Loop Until Math.Abs(fval) < etol Or Double.IsNaN(T) = True Or ecount > maxit_e

            Else
                Do
                    ecount = 0

                    '================================================
                    '== mix vapour and liquid phase =================
                    '================================================
                    Vmix = Vy.MultiplyConstY(V)
                    Vmix = Vmix.AddY(Vx.MultiplyConstY(L))
                    Vz = Vmix.NormalizeY

                    Do
                        Ki = PP.DW_CalcKvalue(Vx, Vy, T, P)

                        i = 0
                        Do
                            If Vz(i) <> 0 Then
                                Vy_ant(i) = Vy(i)
                                Vx_ant(i) = Vx(i)
                                Vy(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * Vint + 1)
                                Vx(i) = Vy(i) / Ki(i)
                            Else
                                Vy(i) = 0
                                Vx(i) = 0
                            End If
                            i += 1
                        Loop Until i = n + 1

                        Vx = Vx.NormalizeY()
                        Vy = Vy.NormalizeY()

                        If Vint <= 0.5 Then

                            stmp4 = Ki.MultiplyY(Vx).SumY
                            'i = 0
                            'stmp4 = 0
                            'Do
                            '    stmp4 = stmp4 + Ki(i) * Vx(i)
                            '    i = i + 1
                            'Loop Until i = n + 1

                            Dim K1(n), K2(n), dKdT(n) As Double

                            K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                            K2 = PP.DW_CalcKvalue(Vx, Vy, T + 0.1, P)

                            dKdT = K2.SubtractY(K1).MultiplyConstY(1 / 0.1)
                            'For i = 0 To n
                            '    dKdT(i) = (K2(i) - K1(i)) / (0.1)
                            'Next

                            dFdT = Vx.MultiplyY(dKdT).SumY
                            'i = 0
                            'dFdT = 0
                            'Do
                            '    dFdT = dFdT + Vx(i) * dKdT(i)
                            '    i = i + 1
                            'Loop Until i = n + 1

                        Else

                            stmp4 = Vy.DivideY(Ki).SumY
                            'i = 0
                            'stmp4 = 0
                            'Do
                            '    stmp4 = stmp4 + Vy(i) / Ki(i)
                            '    i = i + 1
                            'Loop Until i = n + 1

                            Dim K1(n), K2(n), dKdT(n) As Double

                            K1 = PP.DW_CalcKvalue(Vx, Vy, T, P)
                            K2 = PP.DW_CalcKvalue(Vx, Vy, T + 1, P)

                            dKdT = K2.SubtractY(K1)
                            'For i = 0 To n
                            '    dKdT(i) = (K2(i) - K1(i)) / (1)
                            'Next

                            dFdT = -Vy.DivideY(Ki).DivideY(Ki).MultiplyY(dKdT).SumY
                            'i = 0
                            'dFdT = 0
                            'Do
                            '    dFdT = dFdT - Vy(i) / (Ki(i) ^ 2) * dKdT(i)
                            '    i = i + 1
                            'Loop Until i = n + 1

                        End If

                        ecount += 1

                        fval = stmp4 - 1

                        Tant = T
                        deltaT = -fval / dFdT * AF
                        AF *= 1.01
                        If Abs(deltaT) > 0.1 * T Then
                            T = T + Sign(deltaT) * 0.1 * T
                        Else
                            T = T + deltaT
                        End If

                        e1 = Vx.SubtractY(Vx_ant).AbsSumY + Vy.SubtractY(Vy_ant).AbsSumY + Math.Abs(T - Tant)

                        WriteDebugInfo("PV Flash [SLE]: Iteration #" & ecount & ", T = " & T & ", VF = " & V)

                        If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

                    Loop Until (Math.Abs(fval) < etol And e1 < etol) Or Double.IsNaN(T) = True Or ecount > maxit_e

                    '================================================
                    '== mix solid and liquid phase ==================
                    '================================================
                    Vmix = Vs.MultiplyConstY(S)
                    Vmix = Vmix.AddY(Vx.MultiplyConstY(L))
                    Vz = Vmix.NormalizeY

                    '================================================
                    '== Do SLE flash to precipitate solids ==========
                    '================================================
                    Vs_ant = Vs.Clone
                    Dim SL_Result As Object
                    SL_Result = Flash_SL(Vz, P, T, PP)
                    Vx = SL_Result(3)
                    Vs = SL_Result(4)

                    '================================================
                    '== Calculate global phase fractions ============
                    '================================================
                    GL_old = L
                    GS_old = S
                    L = SL_Result(0) * (1 - V)
                    S = SL_Result(1) * (1 - V)

                    '===================================================================
                    '== Calculate vapour fraction relative to vapour/liquid ============
                    '===================================================================
                    Vint = V / (1 - S)
                    If Vint > 1 Then
                        'no liquid left, take some solid to vapour phase
                        Vint = 1
                    End If

                    e1 = 1000 * (Abs(GL_old - L) + Abs(GS_old - S))
                    gcount += 1
                Loop Until e1 < etol Or gcount > maxit_e
            End If



            d2 = Date.Now

            dt = d2 - d1

            If ecount > maxit_e Then Throw New Exception(Calculator.GetLocalString("PropPack_FlashMaxIt2"))

            If PP.AUX_CheckTrivial(Ki) Then Throw New Exception("PV Flash [SLE]: Invalid result: converged to the trivial solution (T = " & T & " ).")

            WriteDebugInfo("PV Flash [SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, Vy, T, ecount, Ki, 0.0#, PP.RET_NullVector, S, Vs}

        End Function

        Public Overrides Function Flash_PSF(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            CompoundProperties = PP.DW_GetConstantProperties

            'Pressure/Solid fraction flash

            If SolidSolution Then
                Return Flash_PSF_SS(Vz, P, V, Tref, PP)
            Else
                Return Flash_PSF_E(Vz, P, V, Tref, PP)
            End If

        End Function

        Public Function Flash_PSF_SS(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim soma_x, soma_s As Double
            Dim L, S, Lf, Sf, T, Tf As Double
            Dim ids As New List(Of String)

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            L = V
            Lf = L
            S = 1 - L
            Lf = 1 - Sf
            Tf = T

            Dim Vn(n) As String, Vx(n), Vs(n), Vx_ant(1), Vs_ant(1), Vp(n), Vp2(n), Ki(n), Ki_ant(n), fi(n), activcoeff(n), activcoeff2(n) As Double
            Dim Vt(n), VTF(n), Tmin, Tmax, dFdT As Double

            Vn = PP.RET_VNAMES()
            VTF = PP.RET_VTF()
            fi = Vz.Clone

            If Tref = 0.0# Then

                i = 0
                Tref = 0
                Do
                    If L = 0 Then
                        Tref = MathEx.Common.Min(VTF)
                    Else
                        Tref += Vz(i) * VTF(i)
                    End If
                    Tmin += 0.1 * Vz(i) * VTF(i)
                    Tmax += 2.0 * Vz(i) * VTF(i)
                    i += 1
                Loop Until i = n + 1

            Else

                Tmin = Tref - 50
                Tmax = Tref + 50

            End If

            T = Tref

            'Calculate Ki`s

            i = 0
            Do
                ids.Add(CompoundProperties(i).Name)
                Vp(i) = PP.AUX_PVAPi(i, T)
                If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                    Ki(i) = Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                Else
                    Ki(i) = 1.0E+20
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            Do
                If Vz(i) <> 0.0# Then
                    Vx(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * L + 1)
                    If Ki(i) <> 0 Then Vs(i) = Vx(i) / Ki(i) Else Vs(i) = Vz(i)
                    If Vs(i) < 0 Then Vs(i) = 0
                    If Vx(i) < 0 Then Vx(i) = 0
                Else
                    Vs(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            soma_x = 0.0#
            soma_s = 0.0#
            Do
                soma_x = soma_x + Vx(i)
                soma_s = soma_s + Vs(i)
                i = i + 1
            Loop Until i = n + 1
            i = 0
            Do
                Vx(i) = Vx(i) / soma_x
                Vs(i) = Vs(i) / soma_s
                i = i + 1
            Loop Until i = n + 1

            Dim marcador3, marcador2, marcador As Integer
            Dim stmp4_ant, stmp4, Tant, fval As Double
            Dim chk As Boolean = False

            ecount = 0
            Do

                marcador3 = 0

                Dim cont_int = 0
                Do

                    Ki_ant = Ki.Clone

                    activcoeff = PP.DW_CalcFugCoeff(Vx, T, P, State.Liquid)

                    For i = 0 To n
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        activcoeff(i) = activcoeff(i) * P / Vp(i)
                        If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                            Ki(i) = (1 / activcoeff(i)) * Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                        Else
                            Ki(i) = 1.0E+20
                        End If
                    Next

                    marcador = 0
                    If stmp4_ant <> 0 Then
                        marcador = 1
                    End If
                    stmp4_ant = stmp4

                    If L = 0 Then
                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Ki(i) * Vs(i)
                            i = i + 1
                        Loop Until i = n + 1
                    Else
                        i = 0
                        stmp4 = 0
                        Do
                            stmp4 = stmp4 + Vx(i) / Ki(i)
                            i = i + 1
                        Loop Until i = n + 1
                    End If

                    If L = 0 Then
                        i = 0
                        Do
                            Vx_ant(i) = Vx(i)
                            Vx(i) = Ki(i) * Vs(i) / stmp4
                            i = i + 1
                        Loop Until i = n + 1
                    Else
                        i = 0
                        Do
                            Vs_ant(i) = Vs(i)
                            Vs(i) = (Vx(i) / Ki(i)) / stmp4
                            i = i + 1
                        Loop Until i = n + 1
                    End If

                    marcador2 = 0
                    If marcador = 1 Then
                        If L = 0 Then
                            If Math.Abs(Vx(0) - Vx_ant(0)) < itol Then
                                marcador2 = 1
                            End If
                        Else
                            If Math.Abs(Vs(0) - Vs_ant(0)) < itol Then
                                marcador2 = 1
                            End If
                        End If
                    End If

                    cont_int = cont_int + 1

                Loop Until marcador2 = 1 Or Double.IsNaN(stmp4) Or cont_int > maxit_i

                Dim K1(n), K2(n), dKdT(n) As Double

                activcoeff = PP.DW_CalcFugCoeff(Vx, T, P, State.Liquid)
                activcoeff2 = PP.DW_CalcFugCoeff(Vx, T + 0.01, P, State.Liquid)

                For i = 0 To n
                    If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                        Vp(i) = PP.AUX_PVAPi(i, T)
                        activcoeff(i) = activcoeff(i) * P / Vp(i)
                        K1(i) = (1 / activcoeff(i)) * Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                        Vp2(i) = PP.AUX_PVAPi(i, T + 0.01)
                        activcoeff2(i) = activcoeff2(i) * P / Vp2(i)
                        K2(i) = (1 / activcoeff2(i)) * Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * (T + 0.01)) * (1 - (T + 0.01) / CompoundProperties(i).TemperatureOfFusion))
                    Else
                        K1(i) = 1.0E+20
                        K2(i) = 1.0E+20
                    End If
                Next

                For i = 0 To n
                    dKdT(i) = (K2(i) - K1(i)) / 0.01
                Next

                fval = stmp4 - 1

                ecount += 1

                i = 0
                dFdT = 0
                Do
                    If L = 0.0# Then
                        dFdT = dFdT + Vs(i) * dKdT(i)
                    Else
                        dFdT = dFdT - Vx(i) / (Ki(i) ^ 2) * dKdT(i)
                    End If
                    i = i + 1
                Loop Until i = n + 1

                Tant = T
                T = T - fval / dFdT
                'If T < Tmin Then T = Tmin
                'If T > Tmax Then T = Tmax

                WriteDebugInfo("PV Flash [NL-SLE]: Iteration #" & ecount & ", T = " & T & ", LF = " & L)

                If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then PP.CurrentMaterialStream.Flowsheet.CheckStatus()

            Loop Until Math.Abs(T - Tant) < 0.01 Or Double.IsNaN(T) = True Or ecount > maxit_e Or Double.IsNaN(T) Or Double.IsInfinity(T)

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PSF Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {L, V, Vx, PP.RET_NullVector, T, ecount, Ki, 0.0#, PP.RET_NullVector, S, Vs}


        End Function

        Public Function Flash_PSF_E(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim i, n, ecount As Integer
            Dim d1, d2 As Date, dt As TimeSpan
            Dim L, S, Lf, Sf, T, Tf As Double
            Dim ids As New List(Of String)

            d1 = Date.Now

            etol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_External_Loop_Tolerance).ToDoubleFromInvariant
            maxit_e = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations)
            itol = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Internal_Loop_Tolerance).ToDoubleFromInvariant
            maxit_i = Me.FlashSettings(Interfaces.Enums.FlashSetting.PTFlash_Maximum_Number_Of_Internal_Iterations)

            n = Vz.Length - 1

            PP = PP
            L = V
            Lf = L
            S = 1 - L
            Lf = 1 - Sf
            Tf = T

            Dim Vn(n) As String, Vx(n), Vs(n), Vx_ant(1), Vs_ant(1), Vp(n), Vp2(n), Ki(n), Ki_ant(n), fi(n), activcoeff(n), activcoeff2(n) As Double
            Dim Vt(n), VTF(n), Tmin, Tmax As Double

            Vn = PP.RET_VNAMES()
            VTF = PP.RET_VTF()
            fi = Vz.Clone

            If Tref = 0.0# Then

                i = 0
                Tref = 0
                Do
                    If L = 0 Then 'L=0
                        Tref = MathEx.Common.Min(VTF)
                    Else
                        Tref += Vz(i) * VTF(i)
                    End If
                    Tmin += 0.1 * Vz(i) * VTF(i)
                    Tmax += 2.0 * Vz(i) * VTF(i)
                    i += 1
                Loop Until i = n + 1

            Else

                Tmin = Tref - 50
                Tmax = Tref + 50

            End If

            T = Tref

            'Calculate Ki`s

            i = 0
            Do
                ids.Add(CompoundProperties(i).Name)
                Vp(i) = PP.AUX_PVAPi(i, T)
                If CompoundProperties(i).TemperatureOfFusion <> 0.0# Then
                    Ki(i) = Exp(-CompoundProperties(i).EnthalpyOfFusionAtTf / (0.00831447 * T) * (1 - T / CompoundProperties(i).TemperatureOfFusion))
                Else
                    Ki(i) = 1.0E+20
                End If
                i += 1
            Loop Until i = n + 1

            i = 0
            Do
                If Vz(i) <> 0.0# Then
                    Vx(i) = Vz(i) * Ki(i) / ((Ki(i) - 1) * L + 1)
                    If Ki(i) <> 0 Then Vs(i) = Vx(i) / Ki(i) Else Vs(i) = Vz(i)
                    If Vs(i) < 0 Then Vs(i) = 0
                    If Vx(i) < 0 Then Vx(i) = 0
                Else
                    Vs(i) = 0
                    Vx(i) = 0
                End If
                i += 1
            Loop Until i = n + 1

            Vx = Vx.NormalizeY
            Vs = Vx.NormalizeY

            Dim chk As Boolean = False

            Dim result As Object

            If PP.AUX_IS_SINGLECOMP(Vz) Then
                T = 0
                For i = 0 To n
                    T += Vz(i) * Me.CompoundProperties(i).TemperatureOfFusion
                Next
                result = Me.Flash_PT(Vz, P, T, PP)
                Return New Object() {result(0), result(1), result(2), result(3), T, 0, PP.RET_NullVector, 0.0#, PP.RET_NullVector, result(7), result(8)}
            End If

            T = 0
            For i = 0 To n
                T += Vz(i) * Me.CompoundProperties(i).TemperatureOfFusion - 30
                VTF(i) = Me.CompoundProperties(i).TemperatureOfFusion
            Next

            ecount = 0

            Dim SF0, SF1, T0, T1 As Double
            T0 = Common.Min(VTF) * 0.6
            T1 = Common.Max(VTF) + 10

            SF0 = Flash_PT(Vz, P, T0, PP)(7)
            SF1 = Flash_PT(Vz, P, T1, PP)(7)

            Do
                T = (T0 + T1) / 2
                Sf = Flash_PT(Vz, P, T, PP)(7)

                If Sf > V Then
                    T0 = T
                    SF0 = Sf
                Else
                    T1 = T
                    SF1 = Sf
                End If
                ecount += 1
            Loop Until (T1 - T0) <= itol

            result = Me.Flash_PT_NL(Vz, P, T, PP)

            d2 = Date.Now

            dt = d2 - d1

            WriteDebugInfo("PSF Flash [NL-SLE]: Converged in " & ecount & " iterations. Time taken: " & dt.TotalMilliseconds & " ms.")

            Return New Object() {result(0), result(1), result(2), result(3), T, ecount, PP.RET_NullVector, 0.0#, PP.RET_NullVector, result(7), result(8)}

        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace
