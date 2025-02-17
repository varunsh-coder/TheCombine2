import { Provider } from "react-redux";
import renderer from "react-test-renderer";
import configureMockStore from "redux-mock-store";

import "tests/reactI18nextMock";

import DefinitionCell from "goals/ReviewEntries/ReviewEntriesComponent/CellComponents/DefinitionCell";
import mockWords from "goals/ReviewEntries/ReviewEntriesComponent/tests/WordsMock";
import { defaultWritingSystem } from "types/writingSystem";

// The multiline Input, TextField cause problems in the mock environment.
jest.mock("@mui/material/Input", () => "div");
jest.mock("@mui/material/TextField", () => "div");

const mockStore = configureMockStore()({
  currentProjectState: {
    project: { analysisWritingSystems: [defaultWritingSystem] },
  },
});
const mockWord = mockWords()[0];

describe("DefinitionCell", () => {
  it("renders sort-stylized", () => {
    renderer.act(() => {
      renderer.create(
        <Provider store={mockStore}>
          <DefinitionCell
            rowData={mockWord}
            value={mockWord.senses}
            sortingByThis
          />
        </Provider>
      );
    });
  });

  it("renders editable", () => {
    renderer.act(() => {
      renderer.create(
        <Provider store={mockStore}>
          <DefinitionCell rowData={mockWord} value={mockWord.senses} editable />
        </Provider>
      );
    });
  });
});
