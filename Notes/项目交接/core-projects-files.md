# TreeGraph 核心项目完整文件清单

## TreeGraph.Blazor.Shared

+-- Common
|   +-- BoolFieldSky.razor
|   +-- PageDialogSky.razor
|   \-- ProgressCircularSky.razor
+-- NodeEav
|   +-- Components
|   |   +-- FieldRenderers
|   |   |   +-- _Imports.razor
|   |   |   +-- ArrayFieldRenderer.razor
|   |   |   +-- BoolField.razor
|   |   |   +-- CompositeField.razor
|   |   |   +-- DateField.razor
|   |   |   +-- DateTimeField.razor
|   |   |   +-- FileField.razor
|   |   |   +-- NumberField.razor
|   |   |   +-- NumericValueDto.cs
|   |   |   +-- SingleChoiceField.razor
|   |   |   +-- StringField.razor
|   |   |   +-- TableField.razor
|   |   |   +-- TimeField.razor
|   |   |   \-- UnitNumberField.razor
|   |   +-- Shared
|   |   |   +-- ArrayFieldEditor.razor
|   |   |   +-- CompositeFieldEditor.razor
|   |   |   +-- CustomTableEditor.razor
|   |   |   +-- DynamicFieldRenderer.razor
|   |   |   +-- EntityHistoryPanel.razor
|   |   |   +-- QueryFilterBuilder.razor
|   |   |   +-- QueryFilterEditor.razor
|   |   |   +-- RenderLeafField.razor
|   |   |   \-- ValueInput.razor
|   |   \-- DynamicForm.razor
|   +-- Pages
|   |   +-- Attributes
|   |   |   \-- AttributesList.razor
|   |   +-- CustomTable
|   |   |   \-- CustomTableEditor.razor
|   |   +-- Entities
|   |   |   +-- EntityEdit.razor
|   |   |   +-- EntityHistory.razor
|   |   |   +-- EntityList.razor
|   |   |   \-- EntitySelector.razor
|   |   +-- Inodes
|   |   |   +-- InodeDetail.razor
|   |   |   +-- InodeEntityEdit.razor
|   |   |   +-- InodeEntityHistory.razor
|   |   |   \-- InodeList.razor
|   |   +-- Metadata
|   |   |   +-- Attributes.razor
|   |   |   +-- CompositeTypes.razor
|   |   |   +-- CustomTables.razor
|   |   |   +-- EntityTypes.razor
|   |   |   +-- OptionSets.razor
|   |   |   \-- Units.razor
|   |   +-- Query
|   |   |   \-- DynamicQuery.razor
|   |   +-- Schema
|   |   |   \-- SchemaViewer.razor
|   |   \-- CustomTables.razor
|   +-- Services
|   |   +-- EavApiClient.cs
|   |   +-- EavFieldValidator.cs
|   |   +-- EntityTypeDisplayService.cs
|   |   +-- FieldValidationError.cs
|   |   +-- FieldValidationRules.cs
|   |   +-- FilterOperatorCatalog.cs
|   |   \-- NumericInput.cs
|   \-- _Imports.razor
+-- Trees
|   +-- StringTree
|   |   +-- Components
|   |   |   +-- StringNodeActionsDialog.razor
|   |   |   +-- StringNodeEditDialog.razor
|   |   |   +-- StringNodeSortDialog.razor
|   |   |   +-- StringNodeViewDialog.razor
|   |   |   +-- StringParentSelectDialog.razor
|   |   |   +-- StringTreeDialogPageSky.razor
|   |   |   +-- StringTreeSky.razor
|   |   |   +-- StringTreeSky.razor.Action.cs
|   |   |   +-- StringTreeSky.razor.cs
|   |   |   +-- StringTreeSky.razor.Loading.cs
|   |   |   \-- StringTreeSky.razor.Node.cs
|   |   +-- Contracts
|   |   |   +-- StringNodeActionResult.cs
|   |   |   +-- StringNodeTemplate.cs
|   |   |   \-- StringParentSelectResult.cs
|   |   +-- Extensions
|   |   |   \-- StringTreeServiceCollectionExtensions.cs
|   |   +-- Models
|   |   |   +-- StringNodeAction.cs
|   |   |   \-- StringNodeMeta.cs
|   |   \-- Services
|   |       +-- ApiStringTreeActionHandler.cs
|   |       +-- ApiStringTreeDataSource.cs
|   |       +-- IStringTreeActionHandler.cs
|   |       +-- IStringTreeDataSource.cs
|   |       +-- NoopStringTreeActionHandler.cs
|   |       \-- StringTreeDialogService.cs
|   \-- TreeSky
|       +-- Attributes
|       |   \-- TreeRouteAttribute.cs
|       +-- Contracts
|       |   +-- NodeAction.cs
|       |   +-- NodeActionResult.cs
|       |   +-- NodeOperationOutcome.cs
|       |   +-- NodeTemplate.cs
|       |   +-- ParentSelectResult.cs
|       |   \-- SortResult.cs
|       +-- Dialogs
|       |   +-- BlazorService.cs
|       |   +-- DialogConfig.cs
|       |   +-- MessageService.cs
|       |   \-- TreeNodeDialogService.cs
|       +-- Extensions
|       |   \-- TreeSkyServiceCollectionExtensions.cs
|       +-- Models
|       |   +-- ApiResponse.cs
|       |   +-- IArchivableTreeNodeBase.cs
|       |   +-- IHierarchyTreeNodeBase.cs
|       |   +-- ITreeNodeBase.cs
|       |   +-- StringTreeNode.cs
|       |   +-- TreeNodeDto.cs
|       |   \-- TreeQueryParams.cs
|       +-- Navigation
|       |   +-- HistoryEntry.cs
|       |   +-- ITreeNavigationHistoryService.cs
|       |   \-- TreeNavigationHistoryService.cs
|       +-- Nodes
|       |   +-- DialogTreeSky.razor
|       |   +-- TreeNodeActionsDialog.razor
|       |   +-- TreeNodeEditDialog.razor
|       |   +-- TreeNodeParentSelectDialog.razor
|       |   +-- TreeNodeSortDialog.razor
|       |   \-- TreeNodeViewDialog.razor
|       +-- Services
|       |   +-- DefaultTreeActionHandler.cs
|       |   +-- DiberyTreeApiClient.cs
|       |   +-- ITreeActionHandler.cs
|       |   \-- ITreeClientService.cs
|       +-- TreeDialogPageSky.razor
|       +-- TreeHelper.cs
|       +-- TreeSelectDialogSky.razor
|       +-- TreeSky.razor
|       +-- TreeSky.razor.Action.cs
|       +-- TreeSky.razor.cs
|       +-- TreeSky.razor.Loading.cs
|       \-- TreeSky.razor.Node.cs
+-- wwwroot
+-- _Imports.razor
+-- README.md
\-- TreeGraph.Blazor.Shared.csproj


## TreeGraph.Blazor

+-- Components
|   +-- Layout
|   |   +-- MainLayout.razor
|   |   +-- MainLayout.razor.css
|   |   +-- NavMenu.razor
|   |   +-- NavMenu.razor.css
|   |   +-- ReconnectModal.razor
|   |   +-- ReconnectModal.razor.css
|   |   \-- ReconnectModal.razor.js
|   +-- Pages
|   |   +-- Counter.razor
|   |   +-- Error.razor
|   |   +-- Home.razor
|   |   +-- NotFound.razor
|   |   +-- StringTreeDemo.razor
|   |   +-- TreeSkyDemo.razor
|   |   \-- Weather.razor
|   +-- _Imports.razor
|   +-- App.razor
|   \-- Routes.razor
+-- Infrastructure
|   \-- NonIdempotentResilience.cs
+-- Properties
|   \-- launchSettings.json
+-- Services
|   \-- DemoTree
|       +-- InMemoryStringTreeActionHandler.cs
|       +-- InMemoryStringTreeDataSource.cs
|       +-- InMemoryStringTreeStore.cs
|       \-- StringTreeClientService.cs
+-- wwwroot
|   +-- lib
|   |   \-- bootstrap
|   |       \-- dist
|   |           +-- css
|   |           |   +-- bootstrap-grid.css
|   |           |   +-- bootstrap-grid.css.map
|   |           |   +-- bootstrap-grid.min.css
|   |           |   +-- bootstrap-grid.min.css.map
|   |           |   +-- bootstrap-grid.rtl.css
|   |           |   +-- bootstrap-grid.rtl.css.map
|   |           |   +-- bootstrap-grid.rtl.min.css
|   |           |   +-- bootstrap-grid.rtl.min.css.map
|   |           |   +-- bootstrap-reboot.css
|   |           |   +-- bootstrap-reboot.css.map
|   |           |   +-- bootstrap-reboot.min.css
|   |           |   +-- bootstrap-reboot.min.css.map
|   |           |   +-- bootstrap-reboot.rtl.css
|   |           |   +-- bootstrap-reboot.rtl.css.map
|   |           |   +-- bootstrap-reboot.rtl.min.css
|   |           |   +-- bootstrap-reboot.rtl.min.css.map
|   |           |   +-- bootstrap-utilities.css
|   |           |   +-- bootstrap-utilities.css.map
|   |           |   +-- bootstrap-utilities.min.css
|   |           |   +-- bootstrap-utilities.min.css.map
|   |           |   +-- bootstrap-utilities.rtl.css
|   |           |   +-- bootstrap-utilities.rtl.css.map
|   |           |   +-- bootstrap-utilities.rtl.min.css
|   |           |   +-- bootstrap-utilities.rtl.min.css.map
|   |           |   +-- bootstrap.css
|   |           |   +-- bootstrap.css.map
|   |           |   +-- bootstrap.min.css
|   |           |   +-- bootstrap.min.css.map
|   |           |   +-- bootstrap.rtl.css
|   |           |   +-- bootstrap.rtl.css.map
|   |           |   +-- bootstrap.rtl.min.css
|   |           |   \-- bootstrap.rtl.min.css.map
|   |           \-- js
|   |               +-- bootstrap.bundle.js
|   |               +-- bootstrap.bundle.js.map
|   |               +-- bootstrap.bundle.min.js
|   |               +-- bootstrap.bundle.min.js.map
|   |               +-- bootstrap.esm.js
|   |               +-- bootstrap.esm.js.map
|   |               +-- bootstrap.esm.min.js
|   |               +-- bootstrap.esm.min.js.map
|   |               +-- bootstrap.js
|   |               +-- bootstrap.js.map
|   |               +-- bootstrap.min.js
|   |               \-- bootstrap.min.js.map
|   +-- app.css
|   \-- favicon.png
+-- appsettings.Development.json
+-- appsettings.json
+-- Program.cs
+-- ReadMe.md
\-- TreeGraph.Blazor.csproj


## TreeGraph.Blazor.Shared.Tests

+-- Attributes
|   \-- TreeRouteAttributeTests.cs
+-- Components
|   +-- StringParentSelectDialogTests.cs
|   +-- StringTreeSkyComponentTests.cs
|   \-- TreeSkyComponentTests.cs
+-- Extensions
|   +-- StringTreeServiceCollectionExtensionsTests.cs
|   \-- TreeSkyServiceCollectionExtensionsTests.cs
+-- Models
|   +-- StringNodeMetaTests.cs
|   \-- StringTreeNodeTests.cs
+-- Navigation
|   \-- TreeNavigationHistoryServiceTests.cs
+-- Services
|   +-- DefaultTreeActionHandlerTests.cs
|   +-- MessageServiceTests.cs
|   +-- NoopStringTreeActionHandlerTests.cs
|   +-- StringTreeDialogServiceTests.cs
|   \-- TreeNodeDialogServiceTests.cs
+-- TreeGraph.Blazor.Shared.Tests.csproj
+-- TreeHelperIterationTests.cs
\-- TreeHelperTests.cs


## TreeGraph.Blazor.E2E.Tests

+-- Base
|   \-- E2ETestBase.cs
+-- Fixtures
|   +-- AspireHealthCheck.cs
|   +-- BlazorHelpers.cs
|   +-- BlazorHelpers.README.md
|   +-- E2ESettings.cs
|   +-- MetadataHelpers.cs
|   \-- PlaywrightFixture.cs
+-- Tests
|   +-- InodeFlowTests.cs
|   +-- MetadataAttributesTests.cs
|   +-- MetadataEntityTypesTests.cs
|   +-- MetadataOptionSetsTests.cs
|   +-- MetadataUnitsTests.cs
|   +-- StringTreeDemoSmokeTests.cs
|   \-- TreeSkyDemoSmokeTests.cs
+-- appsettings.json
+-- TreeGraph.Blazor.E2E.Tests.csproj
\-- xunit.runner.json

