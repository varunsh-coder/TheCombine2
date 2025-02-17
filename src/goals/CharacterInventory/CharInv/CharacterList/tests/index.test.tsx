import { Provider } from "react-redux";
import renderer from "react-test-renderer";
import configureMockStore from "redux-mock-store";

import "tests/reactI18nextMock";

import CharacterList from "goals/CharacterInventory/CharInv/CharacterList";
import CharacterCard from "goals/CharacterInventory/CharInv/CharacterList/CharacterCard";
import { defaultState } from "goals/CharacterInventory/Redux/CharacterInventoryReducer";
import { newCharacterSetEntry } from "goals/CharacterInventory/Redux/CharacterInventoryReduxTypes";

const characterSet = ["q", "w", "e", "r", "t", "y"].map(newCharacterSetEntry);
const mockStore = configureMockStore()({
  characterInventoryState: { ...defaultState, characterSet },
});

let testRenderer: renderer.ReactTestRenderer;

beforeEach(async () => {
  await renderer.act(async () => {
    testRenderer = renderer.create(
      <Provider store={mockStore}>
        <CharacterList />
      </Provider>
    );
  });
});

describe("CharacterList", () => {
  it("renders", () => {
    const chars = testRenderer.root.findAllByType(CharacterCard);
    expect(chars).toHaveLength(characterSet.length);
  });
});
