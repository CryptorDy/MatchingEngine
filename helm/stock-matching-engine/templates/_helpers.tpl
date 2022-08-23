{{/*
Expand the name of the chart.
*/}}
{{- define "stock-matching-engine.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "stock-matching-engine.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "stock-matching-engine.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "stock-matching-engine.labels" -}}
helm.sh/chart: {{ include "stock-matching-engine.chart" . }}
{{ include "stock-matching-engine.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "stock-matching-engine.selectorLabels" -}}
app.kubernetes.io/name: {{ include "stock-matching-engine.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "stock-matching-engine.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "stock-matching-engine.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Environment variables
*/}}
{{- define "app.env-vars" -}}
{{- if or .Values.env.open .Values.env.existingSecretMappings .Values.env.configMaps }}
  env:
  {{- range $k, $v := .Values.env.open }}
  - name: {{ $k }}
    value: "{{ $v }}"
  {{- end }}
  {{- range $secretMapping := .Values.env.existingSecretMappings }}
  {{- range $k, $v := $secretMapping.existingSecret.env }}
  - name: {{ $k }}
    valueFrom:
      secretKeyRef:
        key: {{ $v }}
        name: {{ $secretMapping.existingSecret.name }}
  {{- end }}
  {{- end }}
{{- if .Values.env.configMaps }}
  envFrom:
  {{- range $configMap := .Values.env.configMaps }}
  - configMapRef:
      name: {{ $configMap }}
  {{- end }}
{{- end }}
{{- end }}
{{- end }}